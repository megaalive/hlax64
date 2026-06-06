using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class FieldLoweringHelper
{
    internal static LoweredInstruction LowerFieldLoad(
        string destination,
        IrValue source,
        ProcedureStackMap stack,
        IReadOnlySet<string>? recordPointerParams = null)
    {
        var parsed = FieldAccessEncoding.Parse(source);
        if (!stack.TryGetSlot(parsed.VariableName, out var slot))
            return new LoweredInstruction($"    ; unknown record variable '{parsed.VariableName}'");

        if (recordPointerParams?.Contains(parsed.VariableName) == true)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"    mov rax, {slot}");
            sb.AppendLine($"    {FieldAccessEncoding.EmitLoad(destination, $"[rax+{parsed.Offset}]", parsed.SizeBits)}");
            return new LoweredInstruction(sb.ToString().TrimEnd());
        }

        var mem = FieldAccessEncoding.FormatMem(slot, parsed.Offset);
        return new LoweredInstruction($"    {FieldAccessEncoding.EmitLoad(destination, mem, parsed.SizeBits)}");
    }

    internal static LoweredInstruction LowerFieldStore(
        IrValue destination,
        string source,
        ProcedureStackMap stack,
        IReadOnlySet<string>? recordPointerParams = null)
    {
        var parsed = FieldAccessEncoding.Parse(destination);
        if (!stack.TryGetSlot(parsed.VariableName, out var slot))
            return new LoweredInstruction($"    ; unknown record variable '{parsed.VariableName}'");

        if (recordPointerParams?.Contains(parsed.VariableName) == true)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"    mov rax, {slot}");
            sb.AppendLine($"    {FieldAccessEncoding.EmitStore($"[rax+{parsed.Offset}]", source, parsed.SizeBits)}");
            return new LoweredInstruction(sb.ToString().TrimEnd());
        }

        var mem = FieldAccessEncoding.FormatMem(slot, parsed.Offset);
        return new LoweredInstruction($"    {FieldAccessEncoding.EmitStore(mem, source, parsed.SizeBits)}");
    }
}
