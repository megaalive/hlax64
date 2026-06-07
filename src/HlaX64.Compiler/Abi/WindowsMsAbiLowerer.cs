using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Types;
using HlaX64.Compiler.Builtins;

namespace HlaX64.Compiler.Abi;

public sealed class WindowsMsAbiLowerer : IAbiLowerer
{
    public string Name => "windows-x64-msabi";
    public IReadOnlyList<string> ArgumentRegisters { get; } = new[] { "rcx", "rdx", "r8", "r9" };
    public string ReturnRegister => "rax";
    public IReadOnlyList<string> CallerSaved { get; } = new[] { "rax", "rcx", "rdx", "r8", "r9", "r10", "r11" };
    public IReadOnlyList<string> CalleeSaved { get; } = new[] { "rbx", "rbp", "rdi", "rsi", "r12", "r13", "r14", "r15" };
    public int StackAlignment => 16;

    private readonly List<StringLiteralInfo> _stringLiterals = new();
    private int _stringLabelCounter;
    private readonly Dictionary<string, string> _valueMap = new(StringComparer.OrdinalIgnoreCase);
    private ProcedureStackMap _stackMap = ProcedureStackMap.Build(new IrFunction("_empty"));
    private int _stackOffset;
    private RuntimeMode _mode;
    private List<string> _externs = new();
    private IReadOnlyDictionary<string, GlobalDataSymbol> _globalData =
        new Dictionary<string, GlobalDataSymbol>(StringComparer.OrdinalIgnoreCase);
    private ExternProcedureRegistry _externRegistry = new();
    private ProcedureTypeRegistry _procedureTypes = new();
    private RecordTypeRegistry _recordTypes = new();
    private IrFunction? _currentFunction;

    public IReadOnlyList<StringLiteralInfo> StringLiterals => _stringLiterals;

    public LoweredFunction Lower(IrFunction function, CompilationOptions options,
        IReadOnlyDictionary<string, GlobalDataSymbol>? globalData = null,
        ProcedureTypeRegistry? procedureTypes = null,
        RecordTypeRegistry? recordTypes = null,
        ExternProcedureRegistry? externProcedures = null)
    {
        _globalData = globalData ?? new Dictionary<string, GlobalDataSymbol>(StringComparer.OrdinalIgnoreCase);
        _externRegistry = externProcedures ?? new ExternProcedureRegistry();
        _procedureTypes = procedureTypes ?? new ProcedureTypeRegistry();
        _recordTypes = recordTypes ?? new RecordTypeRegistry();
        _valueMap.Clear();
        _stackOffset = 0;
        _mode = options.RuntimeMode;
        _externs = new List<string>();
        var stackMap = ProcedureStackMap.Build(function);
        foreach (var (name, slot) in stackMap.Slots)
            _valueMap[name] = slot;
        _stackOffset = stackMap.StackOffsetBytes;
        _stackMap = stackMap;
        var lowered = new LoweredFunction(function.Name, isEntryPoint: function.IsEntryPoint)
        {
            IsExport = function.IsExport
        };

        for (int i = 0; i < function.ParameterValues.Count; i++)
            lowered.Parameters.Add(new ParamInfo(function.ParameterValues[i].Name!, i));

        foreach (var irBlock in function.Blocks)
        {
            var loweredBlock = new LoweredBlock(irBlock.Label);
            bool isEntry = function.IsEntryPoint && irBlock == function.EntryBlock;
            bool isProcEntry = !function.IsEntryPoint && irBlock == function.EntryBlock;

            // Emit prologue for procedure entry blocks
            if (isProcEntry)
            {
                loweredBlock.Instructions.Add(new LoweredInstruction("    push rbp"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    mov rbp, rsp"));
                // After push rbp, RSP is 16-byte aligned; Win64 calls need RSP mod 16 == 8.
                var alignedOffset = _stackOffset == 0 ? 0 : ((_stackOffset + 8 + 15) / 16) * 16;
                if (alignedOffset > 0)
                    loweredBlock.Instructions.Add(new LoweredInstruction($"    sub rsp, {alignedOffset}"));

                var paramTypes = function.ParameterTypes.Select(p => (p.Name, p.Type)).ToList();
                var classified = AbiArgumentClassifier.ClassifyParameters(paramTypes, _recordTypes, _procedureTypes);
                AbiArgumentClassifier.AssignWindowsRegisters(classified,
                    out var gprAssign, out var xmmAssign);

                foreach (var (param, gpr) in gprAssign)
                {
                    if (_valueMap.TryGetValue(param.Name, out var slot))
                        loweredBlock.Instructions.Add(new LoweredInstruction($"    mov {slot}, {gpr}"));
                }

                foreach (var (param, xmm) in xmmAssign)
                {
                    if (_valueMap.TryGetValue(param.Name, out var slot))
                    {
                        var mov = param.Class == AbiArgClass.Float32 ? "movss" : "movsd";
                        loweredBlock.Instructions.Add(new LoweredInstruction($"    {mov} {slot}, {xmm}"));
                    }
                }

                var assignedIndices = new HashSet<int>(gprAssign.Select(x => x.Param.Index));
                foreach (var x in xmmAssign)
                    assignedIndices.Add(x.Param.Index);

                for (int i = 0; i < function.ParameterValues.Count; i++)
                {
                    if (assignedIndices.Contains(i))
                        continue;

                    var param = function.ParameterValues[i];
                    if (_valueMap.TryGetValue(param.Name!, out var slot))
                    {
                        var src = ProcedureStackMap.StackParamSource(i, ArgumentRegisters.Count, firstStackArgOffset: 48);
                        loweredBlock.Instructions.Add(new LoweredInstruction($"    mov rax, {src}\n    mov {slot}, rax"));
                    }
                }
            }

            // Emit entry point prologue (_start)
            if (isEntry)
            {
                loweredBlock.Instructions.Add(new LoweredInstruction("    xor ebx, ebx    ; default exit code = 0"));
                // 32-byte shadow + 8 for Win64 call alignment (RSP mod 16 == 8 before call).
                loweredBlock.Instructions.Add(new LoweredInstruction("    sub rsp, 40     ; shadow + align for Win64 calls"));
                _externs.Add("ExitProcess");
            }

            // Lower each IR instruction
            foreach (var inst in irBlock.Instructions)
            {
                _currentFunction = function;
                var loweredInst = LowerInstruction(inst, function);
                loweredInst.IrId = inst.Id;
                loweredInst.SourceLine = inst.SourceLine;
                loweredBlock.Instructions.Add(loweredInst);
            }

            lowered.Blocks.Add(loweredBlock);
        }

        // Add epilogue to the last block that falls through
        if (lowered.Blocks.Count > 0)
        {
            var lastBlock = lowered.Blocks[^1];
            var lastText = lastBlock.Instructions.Count > 0
                ? lastBlock.Instructions[^1].AsmText
                : "";

            bool endsWithBranch = lastText.StartsWith("    jmp")
                                || lastText.StartsWith("    j")
                                || lastText == "    ret";

            if (!endsWithBranch)
            {
                if (function.IsEntryPoint)
                {
                    lastBlock.Instructions.Add(new LoweredInstruction("    add rsp, 40     ; restore entry shadow + align"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    mov rcx, rbx    ; exit code"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    jmp ExitProcess"));
                }
                else if (!function.IsEntryPoint)
                {
                    if (_stackOffset > 0)
                        lastBlock.Instructions.Add(new LoweredInstruction("    mov rsp, rbp"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    pop rbp"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    ret"));
                }
            }
        }

        lowered.StackFrameSize = function.IsEntryPoint
            ? 8
            : _stackOffset == 0 ? 0 : ((_stackOffset + 8 + 15) / 16) * 16;
        lowered.RequiredExterns = _externs;
        return lowered;
    }

    private LoweredInstruction LowerInstruction(IrInstruction inst, IrFunction context)
    {
        return inst.Opcode switch
        {
            IrOpcode.LoadConstant => LowerLoadConstant(inst),
            IrOpcode.Move => LowerMove(inst),
            IrOpcode.Add => LowerBinaryRmw(inst, "add"),
            IrOpcode.Subtract => LowerBinaryRmw(inst, "sub"),
            IrOpcode.Multiply => LowerBinaryRmw(inst, "imul"),
            IrOpcode.Divide => LowerDivide(inst),
            IrOpcode.Modulo => LowerModulo(inst),
            IrOpcode.BitwiseAnd => LowerBinaryRmw(inst, "and"),
            IrOpcode.BitwiseOr => LowerBinaryRmw(inst, "or"),
            IrOpcode.BitwiseXor => LowerBinaryRmw(inst, "xor"),
            IrOpcode.BitwiseNot => LowerUnaryNot(inst),
            IrOpcode.ShiftLeft => LowerShift(inst, "shl"),
            IrOpcode.ShiftRight => LowerShift(inst, "sar"),
            IrOpcode.CompareToBool => LowerCompareToBool(inst),
            IrOpcode.Compare => LowerCompare(inst),
            IrOpcode.Branch => new LoweredInstruction($"    jmp {inst.TargetBlock}"),
            IrOpcode.ConditionalBranch => LowerConditionalBranch(inst),
            IrOpcode.Call => LowerCallInst(inst),
            IrOpcode.Return => new LoweredInstruction("    ret"),
            _ => new LoweredInstruction($"    ; (unlowered) {inst}")
        };
    }

    private LoweredInstruction LowerLoadConstant(IrInstruction inst)
    {
        var dst = ResolveOperand(inst.Destination);
        var imm = inst.Immediate is long l ? l : 0L;

        if (imm == 0)
        {
            var reg = dst;
            return new LoweredInstruction($"    xor {reg}, {reg}    ; zero");
        }
        return new LoweredInstruction($"    mov {dst}, {imm}");
    }

    private LoweredInstruction LowerMove(IrInstruction inst)
    {
        var dstVal = inst.Destination;
        var srcVal = inst.Operands[0];
        var dst = ResolveOperand(dstVal);
        var src = ResolveOperand(srcVal);

        if (inst.Immediate is string sseMnemonic &&
            sseMnemonic is "movsd" or "movss" or "movd" or "movq" or "addsd" or "subsd" or "ucomisd")
            return new LoweredInstruction($"    {sseMnemonic} {dst}, {src}");

        if (inst.Immediate is string avxMnemonic && BuiltinLoweringHelper.IsAvx2Mnemonic(avxMnemonic))
            return new LoweredInstruction(BuiltinLoweringHelper.FormatAvx2Instruction(avxMnemonic, dst, src));

        if (IsMemRef(srcVal))
        {
            var mem = MemoryRefEncoding.Parse(srcVal!);
            return new LoweredInstruction($"    {MemoryRefEncoding.EmitLoad(dst, mem)}");
        }

        if (ArrayIndexEncoding.IsArrayIndex(srcVal))
            return ArrayLoweringHelper.LowerArrayLoad(dst, srcVal!, _stackMap, ResolveOperand, _globalData);

        if (FieldAccessEncoding.IsFieldAccess(srcVal))
            return FieldLoweringHelper.LowerFieldLoad(dst, srcVal!, _stackMap, _currentFunction?.RecordPointerParams);

        if (GlobalDataEncoding.IsGlobalRef(srcVal))
        {
            var gname = GlobalDataEncoding.DecodeName(srcVal!);
            var bits = _globalData.TryGetValue(gname, out var sym) ? sym.Type.BitWidth : 64;
            return new LoweredInstruction($"    mov {dst}, {GlobalDataEncoding.FormatMem(gname, bits)}");
        }

        if (IsMemRef(dstVal))
        {
            var mem = MemoryRefEncoding.Parse(dstVal!);
            return new LoweredInstruction($"    {MemoryRefEncoding.EmitStore(mem, src)}");
        }

        if (ArrayIndexEncoding.IsArrayIndex(dstVal))
            return ArrayLoweringHelper.LowerArrayStore(dstVal!, src, _stackMap, ResolveOperand, _globalData);

        if (FieldAccessEncoding.IsFieldAccess(dstVal))
            return FieldLoweringHelper.LowerFieldStore(dstVal!, src, _stackMap, _currentFunction?.RecordPointerParams);

        if (GlobalDataEncoding.IsGlobalRef(dstVal))
        {
            var gname = GlobalDataEncoding.DecodeName(dstVal!);
            var bits = _globalData.TryGetValue(gname, out var sym) ? sym.Type.BitWidth : 64;
            return new LoweredInstruction($"    mov {GlobalDataEncoding.FormatMem(gname, bits)}, {src}");
        }

        if (AddressRefEncoding.IsStringRef(srcVal))
        {
            var label = EnsureStringLabel(AddressRefEncoding.DecodeString(srcVal!));
            return new LoweredInstruction($"    lea {dst}, [{label}]");
        }

        if (IsAddrRef(srcVal))
        {
            var varName = AddrVariable(srcVal);
            if (_globalData.ContainsKey(varName))
            {
                if (dst.StartsWith('['))
                    return new LoweredInstruction($"    lea rax, [{varName}]\n    mov {dst}, rax");
                return new LoweredInstruction($"    lea {dst}, [{varName}]");
            }
            if (_valueMap.TryGetValue(varName, out var slot))
            {
                if (dst.StartsWith("[rbp", StringComparison.Ordinal))
                    return new LoweredInstruction($"    lea rax, {slot}\n    mov {dst}, rax");
                return new LoweredInstruction($"    lea {dst}, {slot}");
            }
        }

        return new LoweredInstruction($"    mov {dst}, {src}");
    }

    private LoweredInstruction LowerBinaryRmw(IrInstruction inst, string asmMnemonic)
    {
        var dst = ResolveOperand(inst.Destination);
        var src = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : dst;
        return new LoweredInstruction($"    {asmMnemonic} {dst}, {src}");
    }

    private LoweredInstruction LowerDivide(IrInstruction inst)
    {
        var divisor = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : "1";
        var dst = ResolveOperand(inst.Destination);
        var sb = new System.Text.StringBuilder();
        if (!string.Equals(dst, "rax", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"    mov rax, {dst}");
        sb.AppendLine("    cqo");
        sb.AppendLine($"    idiv {divisor}");
        if (!string.Equals(dst, "rax", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"    mov {dst}, rax");
        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private LoweredInstruction LowerModulo(IrInstruction inst)
    {
        var divisor = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : "1";
        var dst = ResolveOperand(inst.Destination);
        var sb = new System.Text.StringBuilder();
        if (!string.Equals(dst, "rax", StringComparison.OrdinalIgnoreCase))
            sb.AppendLine($"    mov rax, {dst}");
        sb.AppendLine("    cqo");
        sb.AppendLine($"    idiv {divisor}");
        sb.AppendLine($"    mov {dst}, rdx");
        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private LoweredInstruction LowerUnaryNot(IrInstruction inst)
    {
        var dst = ResolveOperand(inst.Destination);
        var src = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : dst;
        if (!string.Equals(dst, src, StringComparison.Ordinal))
            return new LoweredInstruction($"    mov {dst}, {src}\n    not {dst}");
        return new LoweredInstruction($"    not {dst}");
    }

    private LoweredInstruction LowerShift(IrInstruction inst, string asmMnemonic)
    {
        var dst = ResolveOperand(inst.Destination);
        var src = inst.Operands.Count > 0 ? inst.Operands[0] : null;
        if (src?.Name?.StartsWith("imm:", StringComparison.Ordinal) == true)
        {
            var count = src.Name.Substring(4);
            return new LoweredInstruction($"    {asmMnemonic} {dst}, {count}");
        }

        var countReg = ResolveOperand(src);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"    mov rcx, {countReg}");
        sb.AppendLine($"    {asmMnemonic} {dst}, cl");
        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private LoweredInstruction LowerCompareToBool(IrInstruction inst)
    {
        var left = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : "0";
        var right = inst.Operands.Count > 1 ? ResolveOperand(inst.Operands[1]) : "0";
        var dst = ResolveOperand(inst.Destination);
        var setcc = inst.CmpKind switch
        {
            CompareKind.Equal => "sete",
            CompareKind.NotEqual => "setne",
            CompareKind.LessThanSigned => "setl",
            CompareKind.LessOrEqualSigned => "setle",
            CompareKind.GreaterThanSigned => "setg",
            CompareKind.GreaterOrEqualSigned => "setge",
            _ => "sete"
        };

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"    cmp {left}, {right}");
        sb.AppendLine($"    {setcc} al");
        if (string.Equals(dst, "rax", StringComparison.OrdinalIgnoreCase))
            sb.Append("    movzx rax, al");
        else
            sb.Append($"    movzx {dst}, al");
        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private static bool IsMemRef(IrValue? value)
        => value?.Name?.StartsWith("mem:", StringComparison.Ordinal) == true;

    private static bool IsAddrRef(IrValue? value)
        => value?.Name?.StartsWith("addr:", StringComparison.Ordinal) == true
           && !AddressRefEncoding.IsStringRef(value);

    private static string AddrVariable(IrValue value) => value.Name![5..];

    private string EnsureStringLabel(string value)
    {
        foreach (var existing in _stringLiterals)
        {
            if (existing.Value == value)
                return existing.Label;
        }

        var label = NewStringLabel();
        _stringLiterals.Add(new StringLiteralInfo(label, value));
        return label;
    }

    private LoweredInstruction LowerCompare(IrInstruction inst)
    {
        var left = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : "0";
        var right = inst.Operands.Count > 1 ? ResolveOperand(inst.Operands[1]) : "0";
        return new LoweredInstruction($"    cmp {left}, {right}");
    }

    private LoweredInstruction LowerConditionalBranch(IrInstruction inst)
    {
        var cc = inst.CmpKind switch
        {
            CompareKind.Equal => "je",
            CompareKind.NotEqual => "jne",
            CompareKind.LessThanSigned => "jl",
            CompareKind.LessThanUnsigned => "jb",
            CompareKind.LessOrEqualSigned => "jle",
            CompareKind.LessOrEqualUnsigned => "jbe",
            CompareKind.GreaterThanSigned => "jg",
            CompareKind.GreaterThanUnsigned => "ja",
            CompareKind.GreaterOrEqualSigned => "jge",
            CompareKind.GreaterOrEqualUnsigned => "jae",
            _ => "je"
        };
        return new LoweredInstruction($"    {cc} {inst.TargetBlock}");
    }

    private LoweredInstruction LowerCallInst(IrInstruction inst)
    {
        if (inst.Immediate is string name && name == "stdout.put")
            return LowerStdoutPut(inst);

        if (inst.Immediate is string builtin && BuiltinNames.IsBuiltin(builtin))
        {
            if (BuiltinLoweringHelper.TryLowerCall(builtin, inst, ResolveOperand, out var asm))
                return new LoweredInstruction(asm);
            return new LoweredInstruction($"    ; (unlowered builtin) {builtin}");
        }

        var sb = new System.Text.StringBuilder();
        AbiCallLoweringHelper.AppendWindowsCall(sb, inst, _currentFunction!, ResolveCallOperand,
            _externRegistry, _procedureTypes, _recordTypes, _externs);
        return new LoweredInstruction(sb.ToString());
    }

    private LoweredInstruction LowerStdoutPut(IrInstruction inst)
    {
        // On Windows, always use library-mode calls to runtime functions.
        // The runtime functions (windows-x64/*.nasm) use Win32 API internally.
        var sb = new System.Text.StringBuilder();
        var args = inst.Operands;

        var savedRegs = new List<(IrValue Arg, string Reg, int SlotOffset)>();
        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            if (IsRegisterRef(val))
                savedRegs.Add((arg, val, savedRegs.Count * 8));
        }

        int saveBytes = savedRegs.Count * 8;
        int saveAlloc = saveBytes == 0 ? 0 : ((saveBytes + 8 + 15) / 16) * 16;
        if (saveAlloc > 0)
        {
            sb.AppendLine($"    sub rsp, {saveAlloc}      ; stdout.put saves (keep Win64 alignment)");
            foreach (var (arg, reg, offset) in savedRegs)
                sb.AppendLine($"    mov [rsp+{offset}], {reg}      ; save for stdout.put");
        }

        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            var rawName = arg.Name;

            if (rawName is null)
                continue;

            if (rawName.StartsWith("str:"))
            {
                var strVal = rawName.Substring(4);
                var label = NewStringLabel();
                _stringLiterals.Add(new StringLiteralInfo(label, strVal));
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (library)");
                sb.AppendLine($"    lea rcx, [{label}]");
                sb.AppendLine("    call stdout_put_str");
            }
            else if (rawName.StartsWith("imm:"))
            {
                var intStr = rawName.Substring(4);
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (constant int literal, library)");
                sb.AppendLine($"    mov rcx, {intStr}");
                sb.AppendLine("    call stdout_put_str");
            }
            else if (rawName == "nl")
            {
                _externs.Add("stdout_put_nl");
                sb.AppendLine("    ; RUNTIME: stdout_put_nl (library)");
                sb.AppendLine("    call stdout_put_nl");
            }
            else if (IsRegisterRef(val))
            {
                var slot = savedRegs.First(s => s.Arg == arg).SlotOffset;
                _externs.Add("stdout_put_int");
                sb.AppendLine($"    ; RUNTIME: stdout_put_int({val}) (library)");
                sb.AppendLine($"    mov rcx, [rsp+{slot}]");
                sb.AppendLine("    call stdout_put_int");
            }
            else if (rawName != null && _valueMap.TryGetValue(rawName, out var slot))
            {
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (local/pointer var)");
                sb.AppendLine($"    mov rcx, {slot}");
                sb.AppendLine("    call stdout_put_str");
            }
            else if (rawName != null && _globalData.ContainsKey(rawName))
            {
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (global pointer)");
                sb.AppendLine($"    mov rcx, [{rawName}]");
                sb.AppendLine("    call stdout_put_str");
            }
            else
            {
                var labelv = NewStringLabel();
                _stringLiterals.Add(new StringLiteralInfo(labelv, rawName ?? ""));
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (library)");
                sb.AppendLine($"    lea rcx, [{labelv}]");
                sb.AppendLine("    call stdout_put_str");
            }
        }

        if (saveAlloc > 0)
            sb.AppendLine($"    add rsp, {saveAlloc}      ; restore stdout.put saves");

        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private string ResolveCallOperand(IrValue? value)
    {
        if (value is null)
            return "0";

        var name = value.Name;
        if (name is null)
            return "0";

        if (AddressRefEncoding.IsStringRef(value))
            return $"rel {EnsureStringLabel(AddressRefEncoding.DecodeString(value))}";

        if (name.StartsWith("addr:", StringComparison.Ordinal))
        {
            var varName = name[5..];
            if (_globalData.ContainsKey(varName))
                return varName;
            if (_valueMap.TryGetValue(varName, out var slot))
            {
                if (slot.StartsWith('[') && slot.EndsWith(']'))
                    return slot[1..^1];
                return slot;
            }
        }

        return ResolveOperand(value);
    }

    private string ResolveOperand(IrValue? value)
    {
        if (value is null)
            return "0";

        var name = value.Name;
        if (name is null)
            return "0";

        if (name.StartsWith("imm:"))
            return name.Substring(4);

        if (IsRegisterName(name))
            return name;

        if (_valueMap.TryGetValue(name, out var offset))
            return offset;

        return name;
    }

    private static bool IsRegisterName(string name)
    {
        var lower = name.ToLowerInvariant();
        if (lower.StartsWith("xmm") || lower.StartsWith("ymm"))
            return true;
        return lower switch
        {
            "rax" or "rbx" or "rcx" or "rdx" or "rsi" or "rdi" or
            "rbp" or "rsp" or "r8" or "r9" or "r10" or "r11" or
            "r12" or "r13" or "r14" or "r15" or
            "eax" or "ebx" or "ecx" or "edx" or "esi" or "edi" or
            "ebp" or "esp" or "r8d" or "r9d" or "r10d" or "r11d" or
            "r12d" or "r13d" or "r14d" or "r15d" or
            "ax" or "bx" or "cx" or "dx" or
            "al" or "bl" or "cl" or "dl" => true,
            _ => false
        };
    }

    private static bool IsRegisterRef(string operand)
    {
        return IsRegisterName(operand.TrimStart('[').TrimEnd(']'));
    }

    private string NewStringLabel()
    {
        return $"str_{_stringLabelCounter++}";
    }
}
