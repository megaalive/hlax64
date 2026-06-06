using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;

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
    private int _stackOffset;
    private RuntimeMode _mode;
    private List<string> _externs = new();
    private bool _stdoutUsed;
    private bool _rbxExitTouched;

    public IReadOnlyList<StringLiteralInfo> StringLiterals => _stringLiterals;

    public LoweredFunction Lower(IrFunction function, CompilationOptions options)
    {
        _labelCounter = 0;
        _stringLabelCounter = 0;
        _valueMap.Clear();
        _stackOffset = 0;
        _mode = options.RuntimeMode;
        _externs = new List<string>();
        _stdoutUsed = false;
        _rbxExitTouched = false;
        var lowered = new LoweredFunction(function.Name, isEntryPoint: function.IsEntryPoint)
        {
            IsExport = function.IsExport
        };

        // Map parameters to stack slots
        for (int i = 0; i < function.ParameterValues.Count; i++)
        {
            var param = function.ParameterValues[i];
            var offset = -(i + 1) * 8;
            _valueMap[param.Name!] = $"[rbp{offset}]";
            lowered.Parameters.Add(new ParamInfo(param.Name!, i));
            _stackOffset = Math.Max(_stackOffset, (i + 1) * 8);
        }

        for (int i = 0; i < function.LocalValues.Count; i++)
        {
            var local = function.LocalValues[i];
            var offset = -(function.ParameterValues.Count + i + 1) * 8;
            _valueMap[local.Name!] = $"[rbp{offset}]";
            _stackOffset = Math.Max(_stackOffset, (function.ParameterValues.Count + i + 1) * 8);
        }

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

                for (int i = 0; i < function.ParameterValues.Count && i < ArgumentRegisters.Count; i++)
                {
                    var param = function.ParameterValues[i];
                    if (_valueMap.TryGetValue(param.Name!, out var slot))
                        loweredBlock.Instructions.Add(new LoweredInstruction($"    mov {slot}, {ArgumentRegisters[i]}"));
                }
            }

            // Emit entry point prologue (_start)
            if (isEntry)
            {
                loweredBlock.Instructions.Add(new LoweredInstruction("    xor ebx, ebx    ; default exit code = 0 (callee-saved)"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    push rbp"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    mov rbp, rsp"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    sub rsp, 8      ; align stack to 16 bytes"));
            }

            // Lower each IR instruction
            foreach (var inst in irBlock.Instructions)
            {
                loweredBlock.Instructions.Add(LowerInstruction(inst, function));
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

        if (IsMemRef(srcVal))
        {
            var reg = MemRefTarget(srcVal);
            return new LoweredInstruction($"    mov {dst}, [{reg}]");
        }

        if (IsMemRef(dstVal))
        {
            var reg = MemRefTarget(dstVal);
            return new LoweredInstruction($"    mov [{reg}], {src}");
        }

        if (IsAddrRef(srcVal))
        {
            var varName = AddrVariable(srcVal);
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
        TrackRbxExitTouch(inst.Destination);
        var src = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : dst;
        return new LoweredInstruction($"    {asmMnemonic} {dst}, {src}");
    }

    private void TrackRbxExitTouch(IrValue? destination)
    {
        if (destination?.Name?.Equals("rbx", StringComparison.OrdinalIgnoreCase) == true)
            _rbxExitTouched = true;
    }

    private static bool IsMemRef(IrValue? value)
        => value?.Name?.StartsWith("mem:", StringComparison.Ordinal) == true;

    private static bool IsAddrRef(IrValue? value)
        => value?.Name?.StartsWith("addr:", StringComparison.Ordinal) == true;

    private static string MemRefTarget(IrValue value) => value.Name![4..].ToLowerInvariant();

    private static string AddrVariable(IrValue value) => value.Name![5..];

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
        {
            return LowerStdoutPut(inst);
        }

        var sb = new System.Text.StringBuilder();

        // Assign arguments to registers
        var args = inst.Operands;
        for (int i = 0; i < args.Count && i < ArgumentRegisters.Count; i++)
        {
            var argVal = ResolveOperand(args[i]);
            sb.AppendLine($"    mov {ArgumentRegisters[i]}, {argVal}");
        }

        sb.Append("    sub rsp, 8      ; align stack to 16 bytes\n");
        sb.Append($"    call {inst.Immediate}\n");
        sb.Append("    add rsp, 8      ; restore stack alignment");

        return new LoweredInstruction(sb.ToString());
    }

    private LoweredInstruction LowerStdoutPut(IrInstruction inst)
    {
        _stdoutUsed = true;
        if (_mode == RuntimeMode.Library)
            return LowerStdoutPutLibrary(inst);

        var sb = new System.Text.StringBuilder();
        var args = inst.Operands;

        sb.AppendLine("    push rcx        ; preserve rcx (syscall clobbers it)");

        // Pass 1: push register values before any syscalls
        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            if (IsRegisterRef(val))
            {
                sb.AppendLine($"    push {val}      ; save for stdout.put");
            }
        }

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
                sb.AppendLine($"    ; RUNTIME: stdout_put_int({val})");
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
        var sb = new System.Text.StringBuilder();
        var args = inst.Operands;
        int regCount = 0;

        // Pass 1: push register values before any calls
        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            if (IsRegisterRef(val))
            {
                sb.AppendLine($"    push {val}      ; save for stdout.put");
                regCount++;
            }
        }

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
                _externs.Add("stdout_put_int");
                sb.AppendLine($"    ; RUNTIME: stdout_put_int({val}) (library)");
                sb.AppendLine("    pop rax         ; get saved register value");
                sb.AppendLine("    mov rdi, rax");
                sb.AppendLine("    call stdout_put_int");
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

        if (regCount > 0)
            sb.Append("    pop rcx           ; restore caller's rcx");

        return new LoweredInstruction(sb.ToString().TrimEnd());
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

        // Register names
        if (IsRegisterName(name))
            return name;

        // Parameter/local variable -> stack offset
        if (_valueMap.TryGetValue(name, out var offset))
            return offset;

        // Unknown identifier -> treat as label/global
        return name;
    }

    private static bool IsRegisterName(string name)
    {
        return name.ToLowerInvariant() switch
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