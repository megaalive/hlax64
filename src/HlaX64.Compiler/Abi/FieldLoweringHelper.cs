using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class FieldLoweringHelper
{
    internal static LoweredInstruction LowerFieldLoad(
        string destination,
        IrValue source,
        ProcedureStackMap stack)
    {
        var parsed = FieldAccessEncoding.Parse(source);
        if (!stack.TryGetSlot(parsed.VariableName, out var slot))
            return new LoweredInstruction($"    ; unknown record variable '{parsed.VariableName}'");

        var mem = FieldAccessEncoding.FormatMem(slot, parsed.Offset);
        return new LoweredInstruction($"    {FieldAccessEncoding.EmitLoad(destination, mem, parsed.SizeBits)}");
    }

    internal static LoweredInstruction LowerFieldStore(
        IrValue destination,
        string source,
        ProcedureStackMap stack)
    {
        var parsed = FieldAccessEncoding.Parse(destination);
        if (!stack.TryGetSlot(parsed.VariableName, out var slot))
            return new LoweredInstruction($"    ; unknown record variable '{parsed.VariableName}'");

        var mem = FieldAccessEncoding.FormatMem(slot, parsed.Offset);
        return new LoweredInstruction($"    {FieldAccessEncoding.EmitStore(mem, source, parsed.SizeBits)}");
    }
}
