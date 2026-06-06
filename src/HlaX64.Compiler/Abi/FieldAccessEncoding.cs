using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class FieldAccessEncoding
{
    internal sealed record Parsed(string VariableName, int Offset, int SizeBits);

    internal static string Encode(string variableName, int offset, int sizeBits)
        => $"field:{variableName}:{offset}:{sizeBits}";

    internal static bool IsFieldAccess(IrValue? value)
        => value?.Name?.StartsWith("field:", StringComparison.Ordinal) == true;

    internal static Parsed Parse(IrValue value)
    {
        var parts = value.Name![6..].Split(':', 3);
        var offset = parts.Length > 1 && int.TryParse(parts[1], out var off) ? off : 0;
        var size = parts.Length > 2 && int.TryParse(parts[2], out var bits) ? bits : 64;
        return new Parsed(parts[0], offset, size);
    }

    internal static string FormatMem(string rbpSlot, int fieldOffset)
    {
        if (fieldOffset == 0)
            return rbpSlot;
        var inner = rbpSlot.Trim('[', ']');
        return $"[{inner}+{fieldOffset}]";
    }

    internal static string EmitLoad(string destination, string mem, int sizeBits)
        => (sizeBits / 8) switch
        {
            1 => $"movzx {destination}, byte {mem}",
            2 => $"movzx {destination}, word {mem}",
            4 => $"mov {destination}, dword {mem}",
            _ => $"mov {destination}, {mem}",
        };

    internal static string EmitStore(string mem, string source, int sizeBits)
        => (sizeBits / 8) switch
        {
            1 => $"mov byte {mem}, {source}",
            2 => $"mov word {mem}, {source}",
            4 => $"mov dword {mem}, {source}",
            _ => $"mov {mem}, {source}",
        };
}
