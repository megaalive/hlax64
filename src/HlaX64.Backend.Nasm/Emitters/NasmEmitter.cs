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
        _sb.AppendLine("    push rbp");
        _sb.AppendLine("    mov rbp, rsp");

        foreach (var stmt in program.Statements)
        {
            EmitStatement(stmt);
        }

        // Exit syscall: mov rax = exit code
        // Exit code is whatever is in rax at program end
        // For programs that don't set rax, use 0
        _sb.AppendLine("    mov rdi, rax    ; exit code from rax");
        _sb.AppendLine("    mov rax, 60     ; sys_exit");
        _sb.AppendLine("    syscall");

        // Data section
        _sb.AppendLine();
        _sb.AppendLine("section .data");
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
        _sb.AppendLine($"{proc.Name}:");
        _sb.AppendLine("    push rbp");
        _sb.AppendLine("    mov rbp, rsp");

        if (proc.Variables.Count > 0)
        {
            var totalSize = proc.Variables.Count * 8;
            _sb.AppendLine($"    sub rsp, {totalSize}");
        }

        // Map params to stack variables (Linux SysV ABI)
        var paramRegisters = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
        for (int i = 0; i < proc.Parameters.Count && i < paramRegisters.Length; i++)
        {
            var offset = -(i + 1) * 8 - 8; // rbp-8, rbp-16, etc.
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

        _sb.AppendLine($"    call {call.Name}");
    }

    private void EmitStdoutPut(List<AstNode> args)
    {
        foreach (var arg in args)
        {
            if (arg is StringLiteralNode strLit)
            {
                var label = $"str_{_labelCounter++}";
                _stringLiterals.Add((label, strLit.Value));
                // sys_write(1, string, length)
                _sb.AppendLine("    mov rax, 1     ; sys_write");
                _sb.AppendLine("    mov rdi, 1     ; stdout");
                _sb.AppendLine($"    lea rsi, [{label}]");
                _sb.AppendLine($"    mov rdx, {strLit.Value.Length}");
                _sb.AppendLine("    syscall");
            }
            else if (arg is IdentifierNode ident && ident.Name == "nl")
            {
                // sys_write(1, newline, 1)
                _sb.AppendLine("    mov rax, 1     ; sys_write");
                _sb.AppendLine("    mov rdi, 1     ; stdout");
                _sb.AppendLine("    lea rsi, [newline]");
                _sb.AppendLine("    mov rdx, 1");
                _sb.AppendLine("    syscall");
            }
            else if (arg is RegisterNode reg)
            {
                // Print register as 64-bit decimal using division method
                var uid = _labelCounter++;
                _sb.AppendLine($"    ; print register {reg.Name} as decimal");
                _sb.AppendLine("    push rcx");
                _sb.AppendLine($"    mov rax, {reg.Name}");
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
                _sb.AppendLine("    ; rdi = digit count, digits on stack (reversed)");
                _sb.AppendLine("    test rdi, rdi");
                _sb.AppendLine($"    jnz .Lbuf_{uid}");
                _sb.AppendLine("    push 0               ; zero digit");
                _sb.AppendLine("    inc rdi");
                _sb.AppendLine($".Lbuf_{uid}:");
                _sb.AppendLine("    mov rdx, rdi         ; byte count");
                _sb.AppendLine("    sub rsp, rdx         ; stack buffer");
                _sb.AppendLine("    mov rcx, rdi");
                _sb.AppendLine("    lea rsi, [rsp]");
                _sb.AppendLine($".Lpop_{uid}:");
                _sb.AppendLine("    pop rax");
                _sb.AppendLine("    add al, '0'");
                _sb.AppendLine("    mov [rsi], al");
                _sb.AppendLine("    inc rsi");
                _sb.AppendLine($"    loop .Lpop_{uid}");
                _sb.AppendLine("    mov rax, 1           ; sys_write");
                _sb.AppendLine("    mov rdi, 1           ; stdout");
                _sb.AppendLine("    lea rsi, [rsp]       ; buffer");
                _sb.AppendLine("    syscall");
                _sb.AppendLine($"    add rsp, rdx         ; restore stack");
                _sb.AppendLine("    pop rcx");
            }
            else if (arg is IntegerLiteralNode intLit)
            {
                // Print integer literal by storing as string in data section
                var label = $"str_{_labelCounter++}";
                var intStr = intLit.Value.ToString();
                _stringLiterals.Add((label, intStr));
                _sb.AppendLine("    mov rax, 1     ; sys_write");
                _sb.AppendLine("    mov rdi, 1     ; stdout");
                _sb.AppendLine($"    lea rsi, [{label}]");
                _sb.AppendLine($"    mov rdx, {intStr.Length}");
                _sb.AppendLine("    syscall");
            }
        }
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

    private static string FormatOperand(AstNode node)
    {
        return node switch
        {
            RegisterNode reg => reg.Name,
            IntegerLiteralNode intLit => intLit.Value.ToString(),
            StringLiteralNode strLit => $"\"{EscapeString(strLit.Value)}\"",
            IdentifierNode ident => ident.Name,
            _ => "0"
        };
    }
}