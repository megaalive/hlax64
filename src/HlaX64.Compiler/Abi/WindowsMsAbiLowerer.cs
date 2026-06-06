using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;

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
    private int _stackOffset;
    private RuntimeMode _mode;
    private List<string> _externs = new();

    public IReadOnlyList<StringLiteralInfo> StringLiterals => _stringLiterals;

    public LoweredFunction Lower(IrFunction function, CompilationOptions options)
    {
        _stringLabelCounter = 0;
        _valueMap.Clear();
        _stackOffset = 0;
        _mode = options.RuntimeMode;
        _externs = new List<string>();
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
                loweredBlock.Instructions.Add(new LoweredInstruction("    xor ebx, ebx    ; default exit code = 0"));
                loweredBlock.Instructions.Add(new LoweredInstruction("    sub rsp, 32     ; shadow space for Win64 calls"));
                _externs.Add("ExitProcess");
            }

            // Lower each IR instruction
            foreach (var inst in irBlock.Instructions)
            {
                loweredBlock.Instructions.Add(LowerInstruction(inst, function));
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
                    lastBlock.Instructions.Add(new LoweredInstruction("    mov ecx, ebx    ; exit code"));
                    lastBlock.Instructions.Add(new LoweredInstruction("    call ExitProcess"));
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
            IrOpcode.Move => LowerBinaryRmw(inst, "mov"),
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
        var imm = inst.Immediate is long l ? l : 0L;

        if (imm == 0)
        {
            var reg = dst;
            return new LoweredInstruction($"    xor {reg}, {reg}    ; zero");
        }
        return new LoweredInstruction($"    mov {dst}, {imm}");
    }

    private LoweredInstruction LowerBinaryRmw(IrInstruction inst, string asmMnemonic)
    {
        var dst = ResolveOperand(inst.Destination);
        var src = inst.Operands.Count > 0 ? ResolveOperand(inst.Operands[0]) : dst;
        return new LoweredInstruction($"    {asmMnemonic} {dst}, {src}");
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
        {
            return LowerStdoutPut(inst);
        }

        var sb = new System.Text.StringBuilder();

        // Assign arguments to registers
        var args = inst.Operands;
        int extraArgs = args.Count > ArgumentRegisters.Count ? args.Count - ArgumentRegisters.Count : 0;

        // Allocate shadow space + space for extra stack args
        int totalAlloc = 32 + extraArgs * 8;
        if (totalAlloc > 0)
            sb.AppendLine($"    sub rsp, {totalAlloc}     ; shadow space" + (extraArgs > 0 ? " + stack args" : ""));

        // Store extra args on stack above shadow space
        for (int i = ArgumentRegisters.Count; i < args.Count; i++)
        {
            var argVal = ResolveOperand(args[i]);
            int stackSlot = 32 + (i - ArgumentRegisters.Count) * 8;
            sb.AppendLine($"    mov qword [rsp+{stackSlot}], {argVal}");
        }

        // Assign register arguments
        for (int i = 0; i < args.Count && i < ArgumentRegisters.Count; i++)
        {
            var argVal = ResolveOperand(args[i]);
            sb.AppendLine($"    mov {ArgumentRegisters[i]}, {argVal}");
        }

        sb.Append($"    call {inst.Immediate}\n");
        if (totalAlloc > 0)
            sb.Append($"    add rsp, {totalAlloc}");

        return new LoweredInstruction(sb.ToString());
    }

    private LoweredInstruction LowerStdoutPut(IrInstruction inst)
    {
        // On Windows, always use library-mode calls to runtime functions.
        // The runtime functions (windows-x64/*.nasm) use Win32 API internally.
        var sb = new System.Text.StringBuilder();
        var args = inst.Operands;
        int regCount = 0;

        foreach (var arg in args)
        {
            var val = ResolveOperand(arg);
            if (IsRegisterRef(val))
            {
                sb.AppendLine($"    push {val}      ; save for stdout.put");
                regCount++;
            }
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
                _externs.Add("stdout_put_int");
                sb.AppendLine($"    ; RUNTIME: stdout_put_int({val}) (library)");
                sb.AppendLine("    pop rax         ; get saved register value");
                sb.AppendLine("    mov rcx, rax");
                sb.AppendLine("    call stdout_put_int");
            }
            else
            {
                var labelv = NewStringLabel();
                _stringLiterals.Add(new StringLiteralInfo(labelv, rawName));
                _externs.Add("stdout_put_str");
                sb.AppendLine("    ; RUNTIME: stdout_put_str (library)");
                sb.AppendLine($"    lea rcx, [{labelv}]");
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
