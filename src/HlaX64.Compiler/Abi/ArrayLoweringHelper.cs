using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class ArrayLoweringHelper
{
    internal static LoweredInstruction LowerArrayLoad(
        string destination,
        IrValue source,
        ProcedureStackMap stack,
        Func<IrValue?, string> resolveOperand)
    {
        var parsed = ArrayIndexEncoding.Parse(source);
        if (!stack.TryGetSlot(parsed.ArrayName, out var slot) || !stack.TryGetLayout(parsed.ArrayName, out var layout))
            return new LoweredInstruction($"    ; unknown array '{parsed.ArrayName}'");

        var elemSize = layout.ElementSizeBytes;
        if (parsed.IndexKind == "lit")
        {
            var mem = ArrayIndexEncoding.FormatMem(slot, parsed, elemSize);
            return new LoweredInstruction($"    {ArrayIndexEncoding.EmitLoad(destination, mem, elemSize)}");
        }

        var sb = new System.Text.StringBuilder();
        var indexReg = PrepareIndexRegister(sb, parsed, resolveOperand);
        var memReg = ArrayIndexEncoding.FormatMem(slot, parsed, elemSize, indexReg);
        sb.Append($"    {ArrayIndexEncoding.EmitLoad(destination, memReg, elemSize)}");
        return new LoweredInstruction(sb.ToString());
    }

    internal static LoweredInstruction LowerArrayStore(
        IrValue destination,
        string source,
        ProcedureStackMap stack,
        Func<IrValue?, string> resolveOperand)
    {
        var parsed = ArrayIndexEncoding.Parse(destination);
        if (!stack.TryGetSlot(parsed.ArrayName, out var slot) || !stack.TryGetLayout(parsed.ArrayName, out var layout))
            return new LoweredInstruction($"    ; unknown array '{parsed.ArrayName}'");

        var elemSize = layout.ElementSizeBytes;
        if (parsed.IndexKind == "lit")
        {
            var mem = ArrayIndexEncoding.FormatMem(slot, parsed, elemSize);
            return new LoweredInstruction($"    {ArrayIndexEncoding.EmitStore(mem, source, elemSize)}");
        }

        var sb = new System.Text.StringBuilder();
        var indexReg = PrepareIndexRegister(sb, parsed, resolveOperand);
        var memReg = ArrayIndexEncoding.FormatMem(slot, parsed, elemSize, indexReg);
        sb.Append($"    {ArrayIndexEncoding.EmitStore(memReg, source, elemSize)}");
        return new LoweredInstruction(sb.ToString());
    }

    private static string PrepareIndexRegister(
        System.Text.StringBuilder sb,
        ArrayIndexEncoding.Parsed parsed,
        Func<IrValue?, string> resolveOperand)
    {
        if (parsed.IndexKind == "reg")
            return parsed.IndexValue;

        if (parsed.IndexKind == "ident")
        {
            var idxSlot = resolveOperand(new IrValue { Name = parsed.IndexValue });
            sb.AppendLine($"    mov r10, {idxSlot}");
            return "r10";
        }

        return "r10";
    }
}
