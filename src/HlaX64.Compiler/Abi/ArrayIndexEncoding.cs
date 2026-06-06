using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class ArrayIndexEncoding
{
    internal sealed record Parsed(string ArrayName, string IndexKind, string IndexValue);

    internal static string Encode(string arrayName, AstNode index) => index switch
    {
        RegisterNode reg => $"arridx:{arrayName}:reg:{reg.Name.ToLowerInvariant()}",
        IntegerLiteralNode lit => $"arridx:{arrayName}:lit:{lit.Value}",
        IdentifierNode id => $"arridx:{arrayName}:ident:{id.Name}",
        _ => $"arridx:{arrayName}:lit:0"
    };

    internal static bool IsArrayIndex(IrValue? value)
        => value?.Name?.StartsWith("arridx:", StringComparison.Ordinal) == true;

    internal static Parsed Parse(IrValue value)
    {
        var parts = value.Name![7..].Split(':', 3);
        return new Parsed(parts[0], parts.Length > 1 ? parts[1] : "lit", parts.Length > 2 ? parts[2] : "0");
    }

    internal static string FormatMem(string rbpSlot, Parsed idx, int elemSize, string? indexRegOverride = null)
    {
        var inner = rbpSlot.Trim('[', ']');
        if (idx.IndexKind == "lit" && long.TryParse(idx.IndexValue, out var lit))
        {
            var byteOff = lit * elemSize;
            return byteOff == 0 ? $"[{inner}]" : $"[{inner}+{byteOff}]";
        }

        var reg = idx.IndexKind switch
        {
            "reg" => idx.IndexValue,
            "ident" => indexRegOverride ?? idx.IndexValue,
            _ => idx.IndexValue
        };
        return elemSize == 1 ? $"[{inner}+{reg}]" : $"[{inner}+{reg}*{elemSize}]";
    }

    internal static string FormatGlobalMem(string label, Parsed idx, int elemSize, string? indexRegOverride = null)
    {
        if (idx.IndexKind == "lit" && long.TryParse(idx.IndexValue, out var lit))
        {
            var byteOff = lit * elemSize;
            return byteOff == 0 ? $"[{label}]" : $"[{label}+{byteOff}]";
        }

        var reg = idx.IndexKind switch
        {
            "reg" => idx.IndexValue,
            "ident" => indexRegOverride ?? idx.IndexValue,
            _ => idx.IndexValue
        };
        return elemSize == 1 ? $"[{label}+{reg}]" : $"[{label}+{reg}*{elemSize}]";
    }

    internal static string EmitLoad(string destination, string mem, int elemSizeBytes)
        => elemSizeBytes switch
        {
            1 => $"movzx {destination}, byte {mem}",
            2 => $"movzx {destination}, word {mem}",
            4 => $"mov {destination}, dword {mem}",
            _ => $"mov {destination}, {mem}",
        };

    internal static string EmitStore(string mem, string source, int elemSizeBytes)
        => elemSizeBytes switch
        {
            1 => $"mov byte {mem}, {source}",
            2 => $"mov word {mem}, {source}",
            4 => $"mov dword {mem}, {source}",
            _ => $"mov {mem}, {source}",
        };

    internal static string EmitLoad(string destination, Parsed idx, string rbpSlot, int elemSize, string indexOperand)
    {
        var mem = FormatMem(rbpSlot, idx, elemSize, indexOperand);
        return EmitLoad(destination, mem, elemSize);
    }

    internal static string EmitStore(Parsed idx, string rbpSlot, int elemSize, string source, string indexOperand)
    {
        var mem = FormatMem(rbpSlot, idx, elemSize, indexOperand);
        return EmitStore(mem, source, elemSize);
    }
}

internal sealed class ProcedureStackMap
{
    private readonly Dictionary<string, string> _slots = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IrLocalLayout> _layouts = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Slots => _slots;
    public IReadOnlyDictionary<string, IrLocalLayout> Layouts => _layouts;
    public int StackOffsetBytes { get; private set; }

    public static ProcedureStackMap Build(IrFunction function)
    {
        var map = new ProcedureStackMap();
        int slotIndex = 0;

        for (int i = 0; i < function.ParameterValues.Count; i++)
        {
            slotIndex++;
            var param = function.ParameterValues[i];
            map._slots[param.Name!] = SlotAt(slotIndex);
            map._layouts[param.Name!] = new IrLocalLayout();
            map.StackOffsetBytes = Math.Max(map.StackOffsetBytes, slotIndex * 8);
        }

        foreach (var local in function.LocalValues)
        {
            var layout = function.LocalLayouts.TryGetValue(local.Name!, out var l)
                ? l
                : new IrLocalLayout();
            map._layouts[local.Name!] = layout;
            map._slots[local.Name!] = SlotAt(slotIndex + 1);
            slotIndex += layout.StackSlots;
            map.StackOffsetBytes = Math.Max(map.StackOffsetBytes, slotIndex * 8);
        }

        return map;
    }

    public bool TryGetSlot(string name, out string slot) => _slots.TryGetValue(name, out slot!);

    public bool TryGetLayout(string name, out IrLocalLayout layout) => _layouts.TryGetValue(name, out layout!);

    private static string SlotAt(int slotIndex) => $"[rbp{-slotIndex * 8}]";

    /// <summary>Callee stack slot for a parameter passed on the stack (after prologue).</summary>
    public static string StackParamSource(int paramIndex, int registerArgCount, int firstStackArgOffset)
        => $"[rbp+{firstStackArgOffset + (paramIndex - registerArgCount) * 8}]";
}
