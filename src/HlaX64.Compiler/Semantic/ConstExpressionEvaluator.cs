using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Semantic;

/// <summary>
/// Evaluates integer const expressions at compile time (unchecked int64 arithmetic).
/// </summary>
public sealed class ConstExpressionEvaluator
{
    private RecordTypeRegistry? _records;

    public void SetRecordTypes(RecordTypeRegistry records) => _records = records;

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

            case DotAccessNode dot:
                var qualified = EnumTypeRegistry.QualifiedName(dot.BaseName, dot.MemberName);
                if (table.TryGetValue(qualified, out value))
                    return true;
                error = new Diagnostic("HLAX0041", DiagnosticSeverity.Error,
                    $"Undefined enum member '{qualified}'",
                    dot.Line, dot.Column);
                return false;

            case SizeofNode sizeofNode:
                if (_records?.TryGet(sizeofNode.TypeName, out var record) == true)
                {
                    value = record.SizeInBytes;
                    return true;
                }
                error = new Diagnostic("HLAX0042", DiagnosticSeverity.Error,
                    $"Unknown record type '{sizeofNode.TypeName}' in sizeof",
                    sizeofNode.Line, sizeofNode.Column);
                return false;

            case OffsetofNode offsetofNode:
                if (_records?.TryGet(offsetofNode.TypeName, out var rec) == true)
                {
                    if (rec.TryGetField(offsetofNode.FieldName, out var field))
                    {
                        value = field.Offset;
                        return true;
                    }
                    error = new Diagnostic("HLAX0043", DiagnosticSeverity.Error,
                        $"Unknown field '{offsetofNode.FieldName}' in record '{offsetofNode.TypeName}'",
                        offsetofNode.Line, offsetofNode.Column);
                    return false;
                }
                error = new Diagnostic("HLAX0044", DiagnosticSeverity.Error,
                    $"Invalid offsetof: unknown record type '{offsetofNode.TypeName}'",
                    offsetofNode.Line, offsetofNode.Column);
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
