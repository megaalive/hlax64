using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Types;
using HlaX64.Compiler.Builtins;

namespace HlaX64.Compiler.Abi;

public sealed class SysVAbiLowerer : IAbiLowerer
{
    public string Name => "linux-x64-sysv";
    public IReadOnlyList<string> ArgumentRegisters { get; } = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
    public string ReturnRegister => "rax";
    public IReadOnlyList<string> CallerSaved { get; } = new[] { "rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11" };
    public IReadOnlyList<string> CalleeSaved { get; } = new[] { "rbx", "rbp", "r12", "r13", "r14", "r15" };
    public int StackAlignment => 16;

        private readonly List<StringLiteralInfo> _stringLiterals = new();
    private int _labelCounter;
    private int _stringLabelCounter;
    private readonly Dictionary<string, string> _valueMap = new(StringComparer.OrdinalIgnoreCase);
    private ProcedureStackMap _stackMap = ProcedureStackMap.Build(new IrFunction("_empty"));
    private int _stackOffset;
    private RuntimeMode _mode;
    private List<string> _externs = new();
    private bool _stdoutUsed;
    private bool _rbxExitTouched;
    private IReadOnlyDictionary<string, GlobalDataSymbol> _globalData =
        new Dictionary<string, GlobalDataSymbol>(StringComparer.OrdinalIgnoreCase);
    private ExternProcedureRegistry _externRegistry = new();
    private ProcedureTypeRegistry _procedureTypes = new();
    private RecordTypeRegistry _recordTypes = new();

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
        _labelCounter = 0;
        _valueMap.Clear();
        _stackOffset = 0;
        _mode = options.RuntimeMode;
        _externs = new List<string>();
        _stdoutUsed = false;
        _rbxExitTouched = false;
        var stackMap = ProcedureStackMap.Build(function);
        foreach (var (name, slot) in stackMap.Slots)
            _valueMap[name] = slot;
        _stackOffset = stackMap.StackOffsetBytes;
        var lowered = new LoweredFunction(function.Name, isEntryPoint: function.IsEntryPoint)
        {
            IsExport = function.IsExport
        };

        // Map parameters and locals to stack slots (arrays consume multiple slots)
        _stackMap = stackMap;
        for (int i = 0; i < function.ParameterValues.Count; i++)
            lowered.Parameters.Add(new ParamInfo(function.ParameterValues[i].Name!, i));

        // Use the function's full block list
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
                // Align stack to 16 bytes: _stackOffset may need rounding up
                var alignedOffset = ((_stackOffset + 15) / 16) * 16;
                if (alignedOffset > 0)
                    loweredBlock.Instructions.Add(new LoweredInstruction($"    sub rsp, {alignedOffset}"));

                var paramTypes = function.ParameterTypes.Select(p => (p.Name, p.Type)).ToList();
                var classified = AbiArgumentClassifier.ClassifyParameters(paramTypes, _recordTypes, _procedureTypes);
                AbiArgumentClassifier.AssignSysVRegisters(classified,
                    out var gprAssign, out var xmmAssign);

                foreach (var (param, gpr) in gprAssign)
                {
                    if (_valueMap.TryGetValue(param.Name, out var slot))
                        loweredBlock.Instructions.Add(new LoweredInstruction($"    {StackMemOperandHelper.EmitMove(slot, gpr)}"));
                }

                foreach (var (param, xmm) in xmmAssign)
                {
                    if (_valueMap.TryGetValue(param.Name, out var slot))
                    {
                        var mov = param.Class == AbiArgClass.Float32 ? "movss" : "movsd";
                        loweredBlock.Instructions.Add(new LoweredInstruction($"    {mov} {slot}, {xmm}"));
                    }
                }

                int regParams = gprAssign.Count + xmmAssign.Count;
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
                        var src = ProcedureStackMap.StackParamSource(i, ArgumentRegisters.Count, firstStackArgOffset: 16);
                        loweredBlock.Instructions.Add(new LoweredInstruction(
                            $"    mov rax, {StackMemOperandHelper.FormatSizedMem(src)}\n    {StackMemOperandHelper.EmitMove(slot, "rax")}"));
                    }
                }
            }

            // Emit entry point prologue (_start)
            if (isEntry)
            {
                if (FunctionUsesArgvRuntime(function))
                {
                    loweredBlock.Instructions.Add(new LoweredInstruction("    mov rdi, rsp    ; kernel argc/argv block"));
                    loweredBlock.Instructions.Add(new LoweredInstruction("    call hlax_argv_save_from_stack"));
                    if (!_externs.Contains("hlax_argv_save_from_stack", StringComparer.OrdinalIgnoreCase))
                        _externs.Add("hlax_argv_save_from_stack");
                }

                loweredBlock.Instructions.Add(new LoweredInstruction("    xor ebx, ebx    ; default exit code = 0 (callee-saved)"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    push rbp"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    mov rbp, rsp"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    sub rsp, 8      ; align stack to 16 bytes"));
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

        // Add epilogue to the last block that falls through (no branch to another block)
        if (lowered.Blocks.Count > 0)
        {
            var lastBlock = lowered.Blocks[^1];
            var lastText = lastBlock.Instructions.Count > 0
                ? lastBlock.Instructions[^1].AsmText
                : "";

            // Don't add epilogue if the block already ends with a jump/ret
            bool endsWithBranch = lastText.StartsWith("    jmp")
                                || lastText.StartsWith("    j")
                                || lastText == "    ret";

            if (!endsWithBranch)
            {
                if (function.IsEntryPoint)
                {
                    if (_stdoutUsed || _rbxExitTouched)
                        lastBlock.Instructions.Add(new LoweredInstruction("    mov rdi, rbx    ; exit code"));
                    else
                        lastBlock.Instructions.Add(new LoweredInstruction("    mov rdi, rax    ; exit code"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    mov rax, 60     ; sys_exit"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    syscall"));
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
            : ((_stackOffset + 15) / 16) * 16;
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
            IrOpcode.InlineAsm => new LoweredInstruction(inst.Immediate as string ?? "    ; inline"),
            _ => new LoweredInstruction($"    ; (unlowered) {inst}")
        };
    }

    private LoweredInstruction LowerLoadConstant(IrInstruction inst)
    {
        var dst = ResolveOperand(inst.Destination);
        TrackRbxExitTouch(inst.Destination);
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
        TrackRbxExitTouch(inst.Destination);
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
            if (StackMemOperandHelper.IsBareMemory(dst))
            {
                const string scratch = "r11";
                var load = MemoryRefEncoding.EmitLoad(scratch, mem);
                return new LoweredInstruction($"    {load}\n    {StackMemOperandHelper.EmitMove(dst, scratch)}");
            }
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
            if (src.StartsWith('['))
                return new LoweredInstruction($"    mov rax, {src}\n    {MemoryRefEncoding.EmitStore(mem, "rax")}");
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
            return new LoweredInstruction($"    lea {dst}, [rel {label}]");
        }

        if (srcVal?.Name?.StartsWith("proc:", StringComparison.Ordinal) == true)
            return new LoweredInstruction($"    lea {dst}, [rel {srcVal.Name[5..]}]");

        if (IsAddrRef(srcVal) && srcVal is not null)
        {
            var varName = AddrVariable(srcVal);
            if (_globalData.ContainsKey(varName))
            {
                if (dst.StartsWith('['))
                    return new LoweredInstruction($"    lea rax, [rel {varName}]\n    mov {dst}, rax");
                return new LoweredInstruction($"    lea {dst}, [rel {varName}]");
            }
            if (_valueMap.TryGetValue(varName, out var slot))
            {
                if (dst.StartsWith("[rbp", StringComparison.Ordinal))
                    return new LoweredInstruction($"    lea rax, {slot}\n    {StackMemOperandHelper.EmitMove(dst, "rax")}");
                return new LoweredInstruction($"    lea {dst}, {slot}");
            }
        }

        return new LoweredInstruction($"    {StackMemOperandHelper.EmitMove(dst, src)}");
    }

    private LoweredInstruction LowerBinaryRmw(IrInstruction inst, string asmMnemonic)
    {
        var dst = ResolveOperand(inst.Destination);
        TrackRbxExitTouch(inst.Destination);
        var src = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : dst;
        return new LoweredInstruction($"    {StackMemOperandHelper.EmitBinary(asmMnemonic, dst, src)}");
    }

    private LoweredInstruction LowerDivide(IrInstruction inst)
    {
        var divisor = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : "1";
        var dst = ResolveOperand(inst.Destination);
        TrackRbxExitTouch(inst.Destination);
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
        TrackRbxExitTouch(inst.Destination);
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
        TrackRbxExitTouch(inst.Destination);
        var src = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : dst;
        if (!string.Equals(dst, src, StringComparison.Ordinal))
            return new LoweredInstruction($"    mov {dst}, {src}\n    not {dst}");
        return new LoweredInstruction($"    not {dst}");
    }

    private LoweredInstruction LowerShift(IrInstruction inst, string asmMnemonic)
    {
        var dst = ResolveOperand(inst.Destination);
        TrackRbxExitTouch(inst.Destination);
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
        TrackRbxExitTouch(inst.Destination);
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
        sb.AppendLine($"    {StackMemOperandHelper.EmitCompare(left, right)}");
        sb.AppendLine($"    {setcc} al");
        if (string.Equals(dst, "rax", StringComparison.OrdinalIgnoreCase))
            sb.Append("    movzx rax, al");
        else
            sb.Append($"    movzx {dst}, al");
        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private void TrackRbxExitTouch(IrValue? destination)
    {
        if (destination?.Name?.Equals("rbx", StringComparison.OrdinalIgnoreCase) == true)
            _rbxExitTouched = true;
    }

    private static bool IsMemRef(IrValue? value)
        => value?.Name?.StartsWith("mem:", StringComparison.Ordinal) == true;

    private static bool IsAddrRef(IrValue? value)
        => value?.Name?.StartsWith("addr:", StringComparison.Ordinal) == true
           && !AddressRefEncoding.IsStringRef(value);

    private static string AddrVariable(IrValue value) => value.Name![5..];

    private void AppendStdoutPutAddrRef(System.Text.StringBuilder sb, IrValue arg, string reg)
    {
        var varName = AddrVariable(arg);
        if (_globalData.ContainsKey(varName))
            sb.AppendLine($"    lea {reg}, [{varName}]");
        else if (_valueMap.TryGetValue(varName, out var slot))
        {
            if (slot.StartsWith('[') && slot.EndsWith(']'))
                sb.AppendLine($"    lea {reg}, {slot}");
            else
                sb.AppendLine($"    mov {reg}, {slot}");
        }
        else
            sb.AppendLine($"    lea {reg}, [{varName}]");
    }

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
        return new LoweredInstruction($"    {StackMemOperandHelper.EmitCompare(left, right)}");
    }

    private LoweredInstruction LowerConditionalBranch(IrInstruction inst)
    {
        var cc = inst.CmpKind switch
        {
            CompareKind.Equal => "je",
            CompareKind.NotEqual => "jne",
            // Deliberate repair exercise defect: invert signed `<` to `>`.
            // SemASM behavioral vectors must reject min_i64 on SysV.
            CompareKind.LessThanSigned => "jg",
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
        if (inst.Immediate is string name && name is "stdout.put" or "stdout.putu")
            return LowerStdoutPut(inst);

        if (inst.Immediate is string builtin && BuiltinNames.IsBuiltin(builtin))
        {
            if (BuiltinLoweringHelper.TryLowerCall(builtin, inst, ResolveOperand, out var asm))
                return new LoweredInstruction(asm);
            return new LoweredInstruction($"    ; (unlowered builtin) {builtin}");
        }

        var sb = new System.Text.StringBuilder();
        AbiCallLoweringHelper.AppendSysVCall(sb, inst, _currentFunction!, ResolveCallOperand,
            _externRegistry, _procedureTypes, _recordTypes, _externs);
        return new LoweredInstruction(sb.ToString());
    }

    private IrFunction? _currentFunction;

    private LoweredInstruction LowerStdoutPut(IrInstruction inst)
    {
        _stdoutUsed = true;
        if (_mode == RuntimeMode.Library)
            return LowerStdoutPutLibrary(inst);

        var unsigned = string.Equals(inst.Immediate as string, "stdout.putu", StringComparison.Ordinal);

        var sb = new System.Text.StringBuilder();
        var args = inst.Operands;

        sb.AppendLine("    push rcx        ; preserve rcx (syscall clobbers it)");

        var savedRegs = new List<string>();
        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            if (IsRegisterRef(val))
                savedRegs.Add(val);
        }

        for (int i = savedRegs.Count - 1; i >= 0; i--)
            sb.AppendLine($"    push {savedRegs[i]}      ; save for stdout.put");

        // Pass 2: emit print code
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
                sb.AppendLine("    ; RUNTIME: stdout_put_str");
                sb.AppendLine("    mov rax, 1     ; sys_write");
                sb.AppendLine("    mov rdi, 1     ; stdout");
                sb.AppendLine($"    lea rsi, [{label}]");
                sb.AppendLine($"    mov rdx, {strVal.Length}");
                sb.AppendLine("    syscall");
            }
            else if (rawName.StartsWith("imm:"))
            {
                var intStr = rawName.Substring(4);
                var label = NewStringLabel();
                _stringLiterals.Add(new StringLiteralInfo(label, intStr));
                sb.AppendLine("    ; RUNTIME: stdout_put_str (constant int literal)");
                sb.AppendLine("    mov rax, 1     ; sys_write");
                sb.AppendLine("    mov rdi, 1     ; stdout");
                sb.AppendLine($"    lea rsi, [{label}]");
                sb.AppendLine($"    mov rdx, {intStr.Length}");
                sb.AppendLine("    syscall");
            }
            else if (rawName == "nl")
            {
                sb.AppendLine("    ; RUNTIME: stdout_put_nl");
                sb.AppendLine("    mov rax, 1     ; sys_write");
                sb.AppendLine("    mov rdi, 1     ; stdout");
                sb.AppendLine("    lea rsi, [newline]");
                sb.AppendLine("    mov rdx, 1");
                sb.AppendLine("    syscall");
            }
            else if (IsRegisterRef(val))
            {
                var uid = _labelCounter++;
                sb.AppendLine($"    ; RUNTIME: stdout_put_{(unsigned ? "uint" : "int")}({val})");
                sb.AppendLine("    pop rax         ; get saved register value");
                sb.AppendLine("    mov r8, 10");
                sb.AppendLine("    mov rdi, 0          ; digit count");
                sb.AppendLine($"    jmp .Lchk_{uid}");
                sb.AppendLine($".Ldiv_{uid}:");
                sb.AppendLine("    xor rdx, rdx");
                sb.AppendLine("    div r8");
                sb.AppendLine("    push rdx             ; digit");
                sb.AppendLine("    inc rdi");
                sb.AppendLine($".Lchk_{uid}:");
                sb.AppendLine("    test rax, rax");
                sb.AppendLine($"    jnz .Ldiv_{uid}");
                sb.AppendLine("    ; rdi = digit count, digits on stack");
                sb.AppendLine("    test rdi, rdi");
                sb.AppendLine($"    jnz .Lbuf_{uid}");
                sb.AppendLine("    push 0               ; zero digit");
                sb.AppendLine("    inc rdi");
                sb.AppendLine($".Lbuf_{uid}:");
                sb.AppendLine("    mov rdx, rdi         ; byte count");
                sb.AppendLine("    sub rsp, rdx         ; stack buffer");
                sb.AppendLine("    mov r9, rdi          ; loop count");
                sb.AppendLine("    lea rsi, [rsp]       ; buffer ptr");
                sb.AppendLine("    lea rdi, [rsp+rdx]   ; ptr to most significant digit");
                sb.AppendLine($".Lascii_{uid}:");
                sb.AppendLine("    mov rax, [rdi]       ; read digit");
                sb.AppendLine("    add al, '0'");
                sb.AppendLine("    mov [rsi], al");
                sb.AppendLine("    inc rsi");
                sb.AppendLine("    add rdi, 8           ; next digit");
                sb.AppendLine("    dec r9");
                sb.AppendLine($"    jnz .Lascii_{uid}");
                sb.AppendLine("    mov r8, rdx          ; save digit count");
                sb.AppendLine("    mov rax, 1           ; sys_write");
                sb.AppendLine("    mov rdi, 1           ; stdout");
                sb.AppendLine("    lea rsi, [rsp]       ; buffer");
                sb.AppendLine("    syscall");
                sb.AppendLine("    lea rsp, [rsp+r8]    ; deallocate buffer");
                sb.AppendLine("    lea rsp, [rsp+r8*8]  ; deallocate digit pushes");
            }
            else if (IsAddrRef(arg))
            {
                var uid = _labelCounter++;
                sb.AppendLine("    ; RUNTIME: stdout_put_str");
                AppendStdoutPutAddrRef(sb, arg, "rsi");
                sb.AppendLine("    mov rax, 1     ; sys_write");
                sb.AppendLine("    mov rdi, 1     ; stdout");
                sb.AppendLine("    xor rdx, rdx");
                sb.AppendLine($".Laddr_strlen_{uid}:");
                sb.AppendLine("    cmp byte [rsi + rdx], 0");
                sb.AppendLine($"    je .Laddr_strlen_done_{uid}");
                sb.AppendLine("    inc rdx");
                sb.AppendLine($"    jmp .Laddr_strlen_{uid}");
                sb.AppendLine($".Laddr_strlen_done_{uid}:");
                sb.AppendLine("    syscall");
            }
            else
            {
                // Assume it's a string literal -> emit label
                var labelv = NewStringLabel();
                _stringLiterals.Add(new StringLiteralInfo(labelv, rawName));
                sb.AppendLine("    ; RUNTIME: stdout_put_str");
                sb.AppendLine("    mov rax, 1     ; sys_write");
                sb.AppendLine("    mov rdi, 1     ; stdout");
                sb.AppendLine($"    lea rsi, [{labelv}]");
                sb.AppendLine($"    mov rdx, {rawName.Length}");
                sb.AppendLine("    syscall");
            }
        }

        sb.AppendLine("    pop rcx         ; restore rcx");

        return new LoweredInstruction(sb.ToString().TrimEnd());
    }

    private LoweredInstruction LowerStdoutPutLibrary(IrInstruction inst)
    {
        var unsigned = string.Equals(inst.Immediate as string, "stdout.putu", StringComparison.Ordinal);
        var intRuntime = unsigned ? "stdout_put_uint" : "stdout_put_int";
        var sb = new System.Text.StringBuilder();
        var args = inst.Operands;

        var savedRegs = new List<string>();
        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            if (IsRegisterRef(val))
                savedRegs.Add(val);
        }

        for (int i = savedRegs.Count - 1; i >= 0; i--)
            sb.AppendLine($"    push {savedRegs[i]}      ; save for stdout.put");

        // Pass 2: emit library call code
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
                sb.AppendLine($"    lea rdi, [{label}]");
                sb.AppendLine("    call stdout_put_str");
            }
            else if (rawName.StartsWith("imm:"))
            {
                var intStr = rawName.Substring(4);
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (constant int literal, library)");
                sb.AppendLine($"    mov rdi, {intStr}");
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
                _externs.Add(intRuntime);
                sb.AppendLine($"    ; RUNTIME: {intRuntime}({val}) (library)");
                sb.AppendLine("    pop rax         ; get saved register value");
                sb.AppendLine("    mov rdi, rax");
                sb.AppendLine($"    call {intRuntime}");
            }
            else if (IsAddrRef(arg))
            {
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (address-of buffer, library)");
                AppendStdoutPutAddrRef(sb, arg, "rdi");
                sb.AppendLine("    call stdout_put_str");
            }
            else
            {
                // String literal via label
                var labelv = NewStringLabel();
                _stringLiterals.Add(new StringLiteralInfo(labelv, rawName));
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (library)");
                sb.AppendLine($"    lea rdi, [{labelv}]");
                sb.AppendLine("    call stdout_put_str");
            }
        }

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

        if (name.StartsWith("str:", StringComparison.Ordinal))
            return $"rel {EnsureStringLabel(name[4..])}";

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

        // Immediate values prefixed with "imm:"
        if (name.StartsWith("imm:"))
            return name.Substring(4);

        if (TryResolveStdFileDescriptor(name, out var fd))
            return fd;

        // Register names
        if (IsRegisterName(name))
            return name;

        // Parameter/local variable -> stack offset
        if (_valueMap.TryGetValue(name, out var offset))
            return offset;

        if (GlobalDataEncoding.IsGlobalRef(value))
        {
            var gname = GlobalDataEncoding.DecodeName(value);
            var bits = _globalData.TryGetValue(gname, out var sym) ? sym.Type.BitWidth : 64;
            return GlobalDataEncoding.FormatMem(gname, bits);
        }

        return name;
    }

    private static bool TryResolveStdFileDescriptor(string name, out string value)
    {
        value = name switch
        {
            "stdin_fd" => "0",
            "stdout_fd" => "1",
            "stderr_fd" => "2",
            _ => ""
        };
        return value.Length != 0;
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

    private static bool FunctionUsesArgvRuntime(IrFunction function)
    {
        foreach (var block in function.Blocks)
        {
            foreach (var inst in block.Instructions)
            {
                if (inst.Opcode != IrOpcode.Call)
                    continue;
                if (inst.Immediate is not string name)
                    continue;
                if (name.StartsWith("hlax_argv_", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("extern:hlax_argv_", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }

}