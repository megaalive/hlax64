using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Abi;

internal static class ArrayLoweringHelper
{
    internal static LoweredInstruction LowerArrayLoad(
        string destination,
        IrValue source,
        ProcedureStackMap stack,
        Func<IrValue?, string> resolveOperand,
        IReadOnlyDictionary<string, GlobalDataSymbol>? globals = null)
    {
        var parsed = ArrayIndexEncoding.Parse(source);
        if (globals != null && globals.TryGetValue(parsed.ArrayName, out var global))
        {
            var elemSize = global.Type.BitWidth / 8;
            if (parsed.IndexKind == "lit")
            {
                var mem = ArrayIndexEncoding.FormatGlobalMem(parsed.ArrayName, parsed, elemSize);
                return new LoweredInstruction($"    {ArrayIndexEncoding.EmitLoad(destination, mem, elemSize)}");
            }

            var sb = new System.Text.StringBuilder();
            var indexReg = PrepareIndexRegister(sb, parsed, resolveOperand);
            AppendGlobalIndexedLoad(sb, parsed.ArrayName, indexReg, elemSize, destination);
            return new LoweredInstruction(sb.ToString().TrimEnd());
        }

        if (!stack.TryGetSlot(parsed.ArrayName, out var slot) || !stack.TryGetLayout(parsed.ArrayName, out var layout))
            return new LoweredInstruction($"    ; unknown array '{parsed.ArrayName}'");

        var elemSizeLocal = layout.ElementSizeBytes;
        if (parsed.IndexKind == "lit")
        {
            var mem = ArrayIndexEncoding.FormatMem(slot, parsed, elemSizeLocal);
            return new LoweredInstruction($"    {ArrayIndexEncoding.EmitLoad(destination, mem, elemSizeLocal)}");
        }

        var sbLocal = new System.Text.StringBuilder();
        var indexRegLocal = PrepareIndexRegister(sbLocal, parsed, resolveOperand);
        var memRegLocal = ArrayIndexEncoding.FormatMem(slot, parsed, elemSizeLocal, indexRegLocal);
        sbLocal.Append($"    {ArrayIndexEncoding.EmitLoad(destination, memRegLocal, elemSizeLocal)}");
        return new LoweredInstruction(sbLocal.ToString());
    }

    internal static LoweredInstruction LowerArrayStore(
        IrValue destination,
        string source,
        ProcedureStackMap stack,
        Func<IrValue?, string> resolveOperand,
        IReadOnlyDictionary<string, GlobalDataSymbol>? globals = null)
    {
        var parsed = ArrayIndexEncoding.Parse(destination);
        if (globals != null && globals.TryGetValue(parsed.ArrayName, out var global))
        {
            var elemSize = global.Type.BitWidth / 8;
            if (parsed.IndexKind == "lit")
            {
                var mem = ArrayIndexEncoding.FormatGlobalMem(parsed.ArrayName, parsed, elemSize);
                return new LoweredInstruction($"    {ArrayIndexEncoding.EmitStore(mem, source, elemSize)}");
            }

            var sb = new System.Text.StringBuilder();
            var indexReg = PrepareIndexRegister(sb, parsed, resolveOperand);
            AppendGlobalIndexedStore(sb, parsed.ArrayName, indexReg, elemSize, source);
            return new LoweredInstruction(sb.ToString().TrimEnd());
        }

        if (!stack.TryGetSlot(parsed.ArrayName, out var slot) || !stack.TryGetLayout(parsed.ArrayName, out var layout))
            return new LoweredInstruction($"    ; unknown array '{parsed.ArrayName}'");

        var elemSizeLocal = layout.ElementSizeBytes;
        if (parsed.IndexKind == "lit")
        {
            var mem = ArrayIndexEncoding.FormatMem(slot, parsed, elemSizeLocal);
            return new LoweredInstruction($"    {ArrayIndexEncoding.EmitStore(mem, source, elemSizeLocal)}");
        }

        var sbLocal = new System.Text.StringBuilder();
        var indexRegLocal = PrepareIndexRegister(sbLocal, parsed, resolveOperand);
        var memRegLocal = ArrayIndexEncoding.FormatMem(slot, parsed, elemSizeLocal, indexRegLocal);
        sbLocal.Append($"    {ArrayIndexEncoding.EmitStore(memRegLocal, source, elemSizeLocal)}");
        return new LoweredInstruction(sbLocal.ToString());
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

    private static void AppendGlobalIndexedLoad(
        System.Text.StringBuilder sb,
        string label,
        string indexReg,
        int elemSize,
        string destination)
    {
        var baseReg = PickScratchRegister(indexReg, destination);
        sb.AppendLine($"    lea {baseReg}, [rel {label}]");
        var mem = elemSize == 1 ? $"[{baseReg} + {indexReg}]" : $"[{baseReg} + {indexReg}*{elemSize}]";
        sb.Append($"    {ArrayIndexEncoding.EmitLoad(destination, mem, elemSize)}");
    }

    private static void AppendGlobalIndexedStore(
        System.Text.StringBuilder sb,
        string label,
        string indexReg,
        int elemSize,
        string source)
    {
        var baseReg = PickScratchRegister(indexReg, source);
        sb.AppendLine($"    lea {baseReg}, [rel {label}]");
        var mem = elemSize == 1 ? $"[{baseReg} + {indexReg}]" : $"[{baseReg} + {indexReg}*{elemSize}]";
        sb.Append($"    {ArrayIndexEncoding.EmitStore(mem, source, elemSize)}");
    }

    private static string PickScratchRegister(params string[] reserved)
    {
        // Avoid r9 — commonly used as a loop bound in examples (e.g. PE #20).
        foreach (var candidate in new[] { "r11", "r10", "r8", "rax" })
        {
            if (reserved.All(r => !string.Equals(r, candidate, StringComparison.OrdinalIgnoreCase)))
                return candidate;
        }

        return "r11";
    }
}
