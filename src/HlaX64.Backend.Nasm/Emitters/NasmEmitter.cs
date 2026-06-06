using HlaX64.Compiler.Ast;
using System.Text;

namespace HlaX64.Backend.Nasm.Emitters;

/// <summary>
/// Emits NASM x64 assembly from an HlaX64 AST.
/// Note: HLA syntax uses mov(source, destination), NASM uses mov destination, source.
/// This emitter reverses operand order for instructions that need it.
/// </summary>
public sealed class NasmEmitter
{
    private readonly StringBuilder _sb;
    private int _labelCounter;

    // Instructions where operand order should be reversed (HLA: src,dst -> NASM: dst,src)
    private static readonly HashSet<string> ReverseOrderInstructions = new()
    {
        "mov", "add", "sub", "imul", "xor", "and", "or", "cmp"
    };

    public NasmEmitter()
    {
        _sb = new StringBuilder();
        _labelCounter = 0;
    }

    public string Emit(ProgramNode program)
    {
        _sb.Clear();
        _labelCounter = 0;

        _sb.AppendLine("bits 64");
        _sb.AppendLine("section .text");
        _sb.AppendLine("global _start");
        _sb.AppendLine();

        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
            {
                EmitProcedure(proc);
            }
        }

        _sb.AppendLine("_start:");
        _sb.AppendLine("    push rbp");
        _sb.AppendLine("    mov rbp, rsp");

        foreach (var stmt in program.Statements)
        {
            EmitStatement(stmt);
        }

        _sb.AppendLine("    ; exit syscall");
        _sb.AppendLine("    mov rax, 60");
        _sb.AppendLine("    xor rdi, rdi");
        _sb.AppendLine("    syscall");

        return _sb.ToString();
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

        var paramRegisters = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
        for (int i = 0; i < proc.Parameters.Count && i < paramRegisters.Length; i++)
        {
            _sb.AppendLine($"    mov [{proc.Parameters[i].Name}], {paramRegisters[i]}");
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

        var op = FormatOperand(instr.Operands[0]);
        _sb.AppendLine($"    {mnemonic} {op}");
    }

    private void EmitCall(CallNode call)
    {
        if (call.Name == "stdout.put")
        {
            EmitStdoutPut(call.Arguments);
            return;
        }

        var argRegisters = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
        for (int i = 0; i < call.Arguments.Count && i < argRegisters.Length; i++)
        {
            var argValue = FormatOperand(call.Arguments[i]);
            _sb.AppendLine($"    mov {argRegisters[i]}, {argValue}");
        }

        var funcName = call.Name;
        if (call.Name.StartsWith("call "))
            funcName = call.Name.Substring(5);

        _sb.AppendLine($"    call {funcName}");
    }

    private void EmitStdoutPut(List<AstNode> args)
    {
        foreach (var arg in args)
        {
            if (arg is StringLiteralNode strLit)
            {
                var label = $"str_{_labelCounter++}";
                _sb.AppendLine($"    ; stdout.put string: \"{strLit.Value}\"");
                _sb.AppendLine($"    mov rax, 1");
                _sb.AppendLine($"    mov rdi, 1");
                _sb.AppendLine($"    lea rsi, [{label}]");
                _sb.AppendLine($"    mov rdx, {strLit.Value.Length}");
                _sb.AppendLine("    syscall");
            }
            else if (arg is IdentifierNode ident && ident.Name == "nl")
            {
                _sb.AppendLine("    ; stdout.put newline");
                _sb.AppendLine("    mov rax, 1");
                _sb.AppendLine("    mov rdi, 1");
                _sb.AppendLine("    lea rsi, [newline]");
                _sb.AppendLine("    mov rdx, 1");
                _sb.AppendLine("    syscall");
            }
            else if (arg is RegisterNode reg)
            {
                _sb.AppendLine($"    ; TODO: print register {reg.Name}");
            }
            else if (arg is IntegerLiteralNode intLit)
            {
                _sb.AppendLine($"    ; TODO: print integer {intLit.Value}");
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
            StringLiteralNode strLit => $"\"{strLit.Value}\"",
            IdentifierNode ident => ident.Name,
            _ => "0"
        };
    }

    public string GenerateDataSection()
    {
        var data = new StringBuilder();
        data.AppendLine();
        data.AppendLine("section .data");
        data.AppendLine("newline db 0x0A");
        return data.ToString();
    }
}