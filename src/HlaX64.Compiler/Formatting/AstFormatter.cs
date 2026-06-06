using System.Text;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Formatting;

/// <summary>
/// Formats HlaX64 source by parsing to AST and re-emitting canonical layout.
/// </summary>
public static class AstFormatter
{
    public static string Format(string source)
    {
        var lexer = new Lexer(source);
        var parser = new Parser(lexer.Tokenize());
        var program = parser.Parse();
        return EmitProgram(program);
    }

    private static string EmitProgram(ProgramNode program)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"program {program.Name};");

        if (program.Includes.Count > 0)
        {
            sb.AppendLine();
            foreach (IncludeNode inc in program.Includes)
                sb.AppendLine($"#include(\"{inc.Path}\")");
        }

        var procedures = program.Statements.OfType<ProcedureNode>().ToList();
        var mainBody = program.Statements.Where(s => s is not ProcedureNode).ToList();

        if (program.Constants.Count > 0)
        {
            sb.AppendLine();
            foreach (ConstBlockNode block in program.Constants)
                EmitConstBlock(sb, block, 0);
        }

        if (procedures.Count > 0)
        {
            sb.AppendLine();
            foreach (var proc in procedures)
                EmitProcedure(sb, proc, 0);
        }

        sb.AppendLine();
        sb.AppendLine($"begin {program.Name};");
        EmitStatements(sb, mainBody, 1);
        sb.AppendLine($"end {program.Name};");

        return sb.ToString();
    }

    private static void EmitProcedure(StringBuilder sb, ProcedureNode proc, int indent)
    {
        var pad = Indent(indent);
        if (proc.IsExport)
            sb.Append($"{pad}export ");
        sb.Append($"{pad}procedure {proc.Name}(");
        sb.Append(string.Join("; ", proc.Parameters.Select(p => $"{p.Name}:{p.Type}")));
        sb.Append(')');
        if (proc.ReturnsRegister != null)
            sb.Append($"; @returns(\"{proc.ReturnsRegister}\")");
        sb.AppendLine(";");

        if (proc.Constants.Count > 0)
        {
            foreach (ConstBlockNode block in proc.Constants)
                EmitConstBlock(sb, block, indent);
        }

        if (proc.Variables.Count > 0)
        {
            sb.AppendLine($"{pad}var");
            foreach (VariableNode v in proc.Variables)
            {
                var typeDecl = v.ArraySizeExpression != null
                    ? $"{v.Type}[{EmitConstExpr(v.ArraySizeExpression)}]"
                    : v.Type;
                sb.AppendLine($"{pad}    {v.Name}:{typeDecl};");
            }
        }

        sb.AppendLine($"{pad}begin {proc.Name};");
        EmitStatements(sb, proc.Body, indent + 1);
        sb.AppendLine($"{pad}end {proc.Name};");
        sb.AppendLine();
    }

    private static void EmitStatements(StringBuilder sb, List<AstNode> statements, int indent)
    {
        foreach (var stmt in statements)
            EmitStatement(sb, stmt, indent);
    }

    private static void EmitStatement(StringBuilder sb, AstNode node, int indent)
    {
        var pad = Indent(indent);
        switch (node)
        {
            case InstructionNode instr:
                sb.AppendLine($"{pad}{instr.Mnemonic}({string.Join(", ", instr.Operands.Select(EmitOperand))});");
                break;
            case CallNode call:
                sb.AppendLine($"{pad}{call.Name}({string.Join(", ", call.Arguments.Select(EmitOperand))});");
                break;
            case IfNode ifNode:
                sb.AppendLine($"{pad}if({EmitCondition(ifNode.Condition)}) then");
                EmitStatements(sb, ifNode.ThenBody, indent + 1);
                if (ifNode.ElseBody.Count > 0)
                {
                    sb.AppendLine($"{pad}else");
                    EmitStatements(sb, ifNode.ElseBody, indent + 1);
                }
                sb.AppendLine($"{pad}endif;");
                break;
            case WhileNode whileNode:
                sb.AppendLine($"{pad}while({EmitCondition(whileNode.Condition)}) do");
                EmitStatements(sb, whileNode.Body, indent + 1);
                sb.AppendLine($"{pad}endwhile;");
                break;
        }
    }

    private static string EmitCondition(AstNode condition)
    {
        if (condition is ComparisonNode comp)
            return $"{EmitOperand(comp.Left)} {comp.Operator} {EmitOperand(comp.Right)}";
        return EmitOperand(condition);
    }

    private static string EmitOperand(AstNode node) => node switch
    {
        RegisterNode r => r.Name,
        IntegerLiteralNode i => i.Value.ToString(),
        StringLiteralNode s => EscapeString(s.Value),
        IdentifierNode id => id.Name,
        _ => "?"
    };

    private static string EscapeString(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\r", "\\r") + "\"";

    private static void EmitConstBlock(StringBuilder sb, ConstBlockNode block, int indent)
    {
        var pad = Indent(indent);
        sb.AppendLine($"{pad}const");
        foreach (var decl in block.Declarations)
            sb.AppendLine($"{pad}    {decl.Name} := {EmitConstExpr(decl.Expression)};");
        sb.AppendLine($"{pad}endconst;");
    }

    private static string EmitConstExpr(AstNode node) => node switch
    {
        IntegerLiteralNode i => i.Value.ToString(),
        IdentifierNode id => id.Name,
        UnaryExprNode u => $"{u.Operator}{EmitConstExpr(u.Operand)}",
        BinaryExprNode b => $"({EmitConstExpr(b.Left)} {b.Operator} {EmitConstExpr(b.Right)})",
        _ => "?"
    };

    private static string Indent(int level) => new(' ', level * 4);
}
