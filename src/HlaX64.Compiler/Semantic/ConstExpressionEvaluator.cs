using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;

namespace HlaX64.Compiler.Semantic;

/// <summary>
/// Evaluates integer const expressions at compile time (unchecked int64 arithmetic).
/// </summary>
public sealed class ConstExpressionEvaluator
{
    public bool TryEvaluate(AstNode node, CompileTimeConstTable table, out long value, out Diagnostic? error)
    {
        error = null;
        value = 0;

        switch (node)
        {
            case IntegerLiteralNode lit:
                value = lit.Value;
                return true;

            case IdentifierNode ident:
                if (table.TryGetValue(ident.Name, out value))
                    return true;
                error = new Diagnostic("HLAX0031", DiagnosticSeverity.Error,
                    $"Undefined constant '{ident.Name}' in compile-time expression",
                    ident.Line, ident.Column);
                return false;

            case UnaryExprNode unary:
                if (!TryEvaluate(unary.Operand, table, out var inner, out error))
                    return false;
                value = unary.Operator switch
                {
                    "-" => unchecked(-inner),
                    "~" => ~inner,
                    _ => inner
                };
                return true;

            case BinaryExprNode binary:
                if (!TryEvaluate(binary.Left, table, out var left, out error))
                    return false;
                if (!TryEvaluate(binary.Right, table, out var right, out error))
                    return false;

                if (binary.Operator == "/" || binary.Operator == "%")
                {
                    if (right == 0)
                    {
                        error = new Diagnostic("HLAX0032", DiagnosticSeverity.Error,
                            "Division by zero in compile-time expression",
                            binary.Line, binary.Column);
                        return false;
                    }
                }

                try
                {
                    value = binary.Operator switch
                    {
                        "+" => checked(left + right),
                        "-" => checked(left - right),
                        "*" => checked(left * right),
                        "/" => left / right,
                        "%" => left % right,
                        "&" => left & right,
                        "|" => left | right,
                        "^" => left ^ right,
                        "<<" => left << (int)(right & 63),
                        ">>" => left >> (int)(right & 63),
                        _ => left
                    };
                }
                catch (OverflowException)
                {
                    error = new Diagnostic("HLAX0033", DiagnosticSeverity.Error,
                        "Compile-time expression overflow (result does not fit in int64)",
                        binary.Line, binary.Column);
                    return false;
                }

                return true;

            default:
                error = new Diagnostic("HLAX0031", DiagnosticSeverity.Error,
                    "Invalid compile-time expression",
                    node.Line, node.Column);
                return false;
        }
    }
}
