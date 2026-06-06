using HlaX64.Compiler.Ast;
using System.Text;

namespace HlaX64.Backend.Nasm.Emitters;

/// <summary>
/// Emits NASM x64 assembly from an HlaX64 AST.
/// HLA syntax: mov(source, dst) -> NASM: mov dst, source
/// </summary>
public sealed class NasmEmitter
{
    private readonly StringBuilder _sb;
    private int _labelCounter;

    // Collects string literals for the data section
    private readonly List<(string Label, string Value)> _stringLiterals;

    // Current procedure's parameter/variable name -> stack offset (e.g., "[rbp-8]")
    private readonly Dictionary<string, string> _localOffsets = new(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> ReverseOrderInstructions = new()
    {
        "mov", "add", "sub", "imul", "xor", "and", "or", "cmp"
    };

    public NasmEmitter()
    {
        _sb = new StringBuilder();
        _labelCounter = 0;
        _stringLiterals = new List<(string, string)>();
    }

    public string Emit(ProgramNode program)
    {
        _sb.Clear();
        _labelCounter = 0;
        _stringLiterals.Clear();

        // NASM header
        _sb.AppendLine("bits 64");
        _sb.AppendLine("section .text");
        _sb.AppendLine("global _start");
        _sb.AppendLine();

        // Procedure declarations
        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
            {
                EmitProcedure(proc);
            }
        }

        // Main entry point
        _sb.AppendLine("_start:");
        _sb.AppendLine("    xor ebx, ebx    ; default exit code = 0 (rbx = callee-saved)");
        _sb.AppendLine("    push rbp");
        _sb.AppendLine("    mov rbp, rsp");

        foreach (var stmt in program.Statements)
        {
            EmitStatement(stmt);
        }

        // Exit syscall: rbx holds exit code (callee-saved, preserved
        // across calls). Users set it with mov(exitCode, rbx).
        _sb.AppendLine("    mov rdi, rbx    ; exit code");
        _sb.AppendLine("    mov rax, 60     ; sys_exit");
        _sb.AppendLine("    syscall");

        // Data section
        _sb.AppendLine();
        _sb.AppendLine("section .data");
        _sb.AppendLine("; RUNTIME: newline constant lives in src/HlaX64.Runtime/linux-x64/stdout.nasm");
        _sb.AppendLine("newline db 0x0A");
        foreach (var (label, value) in _stringLiterals)
        {
            _sb.AppendLine($"{label} db \"{EscapeString(value)}\", 0");
        }

        return _sb.ToString();
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }

    private void EmitProcedure(ProcedureNode proc)
    {
        _localOffsets.Clear();

        _sb.AppendLine($"{proc.Name}:");
        _sb.AppendLine("    push rbp");
        _sb.AppendLine("    mov rbp, rsp");

        // Build parameter/variable name -> stack offset mapping
        var paramRegisters = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
        int slotIndex = 0;

        // Parameters (after return address and saved rbp)
        for (int i = 0; i < proc.Parameters.Count && i < paramRegisters.Length; i++)
        {
            var offset = -(slotIndex + 1) * 8;
            _localOffsets[proc.Parameters[i].Name] = $"[rbp{offset}]";
            slotIndex++;
        }

        // Variables (below parameters on stack)
        for (int i = 0; i < proc.Variables.Count; i++)
        {
            var offset = -(slotIndex + 1) * 8;
            if (proc.Variables[i] is VariableNode varNode)
                _localOffsets[varNode.Name] = $"[rbp{offset}]";
            slotIndex++;
        }

        // Allocate stack space for variables
        var varCount = proc.Variables.Count;
        if (varCount > 0)
        {
            _sb.AppendLine($"    sub rsp, {varCount * 8}");
        }

        // Store parameter registers into stack slots
        for (int i = 0; i < proc.Parameters.Count && i < paramRegisters.Length; i++)
        {
            var offset = -(i + 1) * 8;
            _sb.AppendLine($"    mov [rbp{offset}], {paramRegisters[i]}");
        }

        foreach (var stmt in proc.Body)
        {
            EmitStatement(stmt);
        }

        _sb.AppendLine("    pop rbp");
        _sb.AppendLine("    ret");
        _sb.AppendLine();
    }

    private void EmitStatement(AstNode node)
    {
        switch (node)
        {
            case InstructionNode instr:
                EmitInstruction(instr);
                break;
            case CallNode call:
                EmitCall(call);
                break;
            case IfNode ifNode:
                EmitIf(ifNode);
                break;
            case WhileNode whileNode:
                EmitWhile(whileNode);
                break;
            case IncludeNode:
                // #include directives are documentation-only for MVP.
                // Runtime prototypes live in src/HlaX64.Runtime/include/stdlib64.hhf
                // and become real calls when the HlaX64.Runtime project is linked in.
                break;
        }
    }

    private void EmitInstruction(InstructionNode instr)
    {
        var mnemonic = instr.Mnemonic.ToLowerInvariant();

        if (instr.Operands.Count == 0)
        {
            _sb.AppendLine($"    {mnemonic}");
            return;
        }

        if (instr.Operands.Count == 2)
        {
            var op1 = FormatOperand(instr.Operands[0]);
            var op2 = FormatOperand(instr.Operands[1]);

            if (ReverseOrderInstructions.Contains(mnemonic))
            {
                _sb.AppendLine($"    {mnemonic} {op2}, {op1}");
            }
            else
            {
                _sb.AppendLine($"    {mnemonic} {op1}, {op2}");
            }
            return;
        }

        if (instr.Operands.Count == 1)
        {
            var op = FormatOperand(instr.Operands[0]);
            _sb.AppendLine($"    {mnemonic} {op}");
        }
    }

    private void EmitCall(CallNode call)
    {
        if (call.Name == "stdout.put")
        {
            EmitStdoutPut(call.Arguments);
            return;
        }

        // Linux SysV ABI: set up arguments in registers
        var argRegisters = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
        for (int i = 0; i < call.Arguments.Count && i < argRegisters.Length; i++)
        {
            var argValue = FormatOperand(call.Arguments[i]);
            _sb.AppendLine($"    mov {argRegisters[i]}, {argValue}");
        }

        // Stack alignment: ensure 16-byte alignment before call
        // After push rbp at entry, RSP is 16-byte aligned.
        // Each push/alloc modifies RSP. Use a runtime alignment check:
        // After saving args, sub rsp,8 then call, then add rsp,8 after ret
        // For simplicity with small programs, align to 16 bytes:
        _sb.AppendLine("    sub rsp, 8      ; align stack to 16 bytes");
        _sb.AppendLine($"    call {call.Name}");
        _sb.AppendLine("    add rsp, 8      ; restore stack alignment");
    }

    private void EmitStdoutPut(List<AstNode> args)
    {
        if (args.Count == 0) return;

        // Save rcx before any syscall or stack manipulation
        _sb.AppendLine("    push rcx          ; save caller's rcx");

        // Pass 1: push all register argument values before any syscalls
        // (string sys_write clobbers RAX, which is often the register to print)
        int regCount = 0;
        foreach (var arg in args)
        {
            if (arg is RegisterNode reg)
            {
                _sb.AppendLine($"    push {reg.Name}      ; save for stdout.put");
                regCount++;
            }
        }

        // Pass 2: emit print code for each argument
        foreach (var arg in args)
        {
            switch (arg)
            {
                case StringLiteralNode strLit:
                {
                    var label = $"str_{_labelCounter++}";
                    _stringLiterals.Add((label, strLit.Value));
                    _sb.AppendLine("    ; RUNTIME: stdout_put_str");
                    _sb.AppendLine("    mov rax, 1     ; sys_write");
                    _sb.AppendLine("    mov rdi, 1     ; stdout");
                    _sb.AppendLine($"    lea rsi, [{label}]");
                    _sb.AppendLine($"    mov rdx, {strLit.Value.Length}");
                    _sb.AppendLine("    syscall");
                    break;
                }
                case IdentifierNode ident when ident.Name == "nl":
                {
                    _sb.AppendLine("    ; RUNTIME: stdout_put_nl");
                    _sb.AppendLine("    mov rax, 1     ; sys_write");
                    _sb.AppendLine("    mov rdi, 1     ; stdout");
                    _sb.AppendLine("    lea rsi, [newline]");
                    _sb.AppendLine("    mov rdx, 1");
                    _sb.AppendLine("    syscall");
                    break;
                }
                case RegisterNode reg:
                {
                    var uid = _labelCounter++;
                    _sb.AppendLine($"    ; RUNTIME: stdout_put_int({reg.Name})");
                    _sb.AppendLine("    pop rax         ; get saved register value");
                    _sb.AppendLine("    mov rcx, 10");
                    _sb.AppendLine("    mov rdi, 0          ; digit count");
                    _sb.AppendLine($"    jmp .Lchk_{uid}");
                    _sb.AppendLine($".Ldiv_{uid}:");
                    _sb.AppendLine("    xor rdx, rdx");
                    _sb.AppendLine("    div rcx");
                    _sb.AppendLine("    push rdx             ; digit");
                    _sb.AppendLine("    inc rdi");
                    _sb.AppendLine($".Lchk_{uid}:");
                    _sb.AppendLine("    test rax, rax");
                    _sb.AppendLine($"    jnz .Ldiv_{uid}");
                    _sb.AppendLine("    ; rdi = digit count, digits on stack");
                    _sb.AppendLine("    test rdi, rdi");
                    _sb.AppendLine($"    jnz .Lbuf_{uid}");
                    _sb.AppendLine("    push 0               ; zero digit");
                    _sb.AppendLine("    inc rdi");
                    _sb.AppendLine($".Lbuf_{uid}:");
                    _sb.AppendLine("    mov rdx, rdi         ; byte count");
                    _sb.AppendLine("    sub rsp, rdx         ; stack buffer");
                    _sb.AppendLine("    mov rcx, rdi         ; loop count");
                    _sb.AppendLine("    lea rsi, [rsp]       ; buffer ptr");
                    _sb.AppendLine("    lea r9, [rsp+rdx]    ; ptr to most significant digit");
                    _sb.AppendLine($".Lascii_{uid}:");
                    _sb.AppendLine("    mov rax, [r9]        ; read digit");
                    _sb.AppendLine("    add al, '0'");
                    _sb.AppendLine("    mov [rsi], al");
                    _sb.AppendLine("    inc rsi");
                    _sb.AppendLine("    add r9, 8            ; next digit");
                    _sb.AppendLine($"    loop .Lascii_{uid}");
                    _sb.AppendLine("    mov r8, rdx          ; save digit count");
                    _sb.AppendLine("    mov rax, 1           ; sys_write");
                    _sb.AppendLine("    mov rdi, 1           ; stdout");
                    _sb.AppendLine("    lea rsi, [rsp]       ; buffer");
                    _sb.AppendLine("    syscall");
                    _sb.AppendLine("    lea rsp, [rsp+r8]    ; deallocate buffer");
                    _sb.AppendLine("    lea rsp, [rsp+r8*8]  ; deallocate digit pushes");
                    break;
                }
                case IntegerLiteralNode intLit:
                {
                    var label = $"str_{_labelCounter++}";
                    var intStr = intLit.Value.ToString();
                    _stringLiterals.Add((label, intStr));
                    _sb.AppendLine("    ; RUNTIME: stdout_put_str (constant int literal)");
                    _sb.AppendLine("    mov rax, 1     ; sys_write");
                    _sb.AppendLine("    mov rdi, 1     ; stdout");
                    _sb.AppendLine($"    lea rsi, [{label}]");
                    _sb.AppendLine($"    mov rdx, {intStr.Length}");
                    _sb.AppendLine("    syscall");
                    break;
                }
            }
        }
        _sb.AppendLine("    pop rcx           ; restore caller's rcx");
    }

    private void EmitIf(IfNode ifNode)
    {
        var elseLabel = $"else_{_labelCounter}";
        var endLabel = $"endif_{_labelCounter++}";

        EmitCondition(ifNode.Condition, elseLabel);

        foreach (var stmt in ifNode.ThenBody)
            EmitStatement(stmt);
        _sb.AppendLine($"    jmp {endLabel}");

        _sb.AppendLine($"{elseLabel}:");
        foreach (var stmt in ifNode.ElseBody)
            EmitStatement(stmt);

        _sb.AppendLine($"{endLabel}:");
    }

    private void EmitWhile(WhileNode whileNode)
    {
        var startLabel = $"while_{_labelCounter}";
        var endLabel = $"endwhile_{_labelCounter++}";

        _sb.AppendLine($"{startLabel}:");

        if (whileNode.Condition is ComparisonNode comp)
        {
            var left = FormatOperand(comp.Left);
            var right = FormatOperand(comp.Right);
            _sb.AppendLine($"    cmp {left}, {right}");

            var jumpOp = comp.Operator switch
            {
                "=" => "jne",
                "<" => "jge",
                ">" => "jle",
                _ => "jne"
            };
            _sb.AppendLine($"    {jumpOp} {endLabel}");
        }

        foreach (var stmt in whileNode.Body)
            EmitStatement(stmt);

        _sb.AppendLine($"    jmp {startLabel}");
        _sb.AppendLine($"{endLabel}:");
    }

    private void EmitCondition(AstNode condition, string elseLabel)
    {
        if (condition is ComparisonNode comp)
        {
            var left = FormatOperand(comp.Left);
            var right = FormatOperand(comp.Right);
            _sb.AppendLine($"    cmp {left}, {right}");

            var jumpOp = comp.Operator switch
            {
                "=" => "jne",
                "<" => "jge",
                ">" => "jle",
                _ => "jne"
            };
            _sb.AppendLine($"    {jumpOp} {elseLabel}");
        }
    }

    private string FormatOperand(AstNode node)
    {
        return node switch
        {
            RegisterNode reg => reg.Name,
            IntegerLiteralNode intLit => intLit.Value.ToString(),
            StringLiteralNode strLit => $"\"{EscapeString(strLit.Value)}\"",
            IdentifierNode ident => _localOffsets.TryGetValue(ident.Name, out var offset) ? offset : ident.Name,
            _ => "0"
        };
    }
}