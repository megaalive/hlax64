using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class MemoryRefEncoding
{
    internal sealed record Parsed(string Register, long Offset, int SizeBits);

    internal static string Encode(string register, long offset, int sizeBits)
        => $"mem:{register.ToLowerInvariant()}:{offset}:{sizeBits}";

    internal static Parsed Parse(IrValue value)
    {
        var payload = value.Name![4..];
        var parts = payload.Split(':');
        var reg = parts[0];
        if (parts.Length == 1)
            return new Parsed(reg, 0, 64);
        long offset = long.TryParse(parts[1], out var off) ? off : 0;
        int size = parts.Length > 2 && int.TryParse(parts[2], out var bits) ? bits : 64;
        return new Parsed(reg, offset, size);
    }

    internal static string FormatAddress(Parsed mem)
    {
        if (mem.Offset == 0)
            return mem.Register;
        if (mem.Offset > 0)
            return $"{mem.Register}+{mem.Offset}";
        return $"{mem.Register}{mem.Offset}";
    }

    internal static string EmitLoad(string destination, Parsed mem)
    {
        var addr = FormatAddress(mem);
        return mem.SizeBits switch
        {
            8 => $"movzx {destination}, byte [{addr}]",
            16 => $"movzx {destination}, word [{addr}]",
            32 => $"mov {destination}, dword [{addr}]",
            _ => $"mov {destination}, [{addr}]",
        };
    }

    internal static string EmitStore(Parsed mem, string source)
    {
        var addr = FormatAddress(mem);
        return mem.SizeBits switch
        {
            8 => $"mov byte [{addr}], {source}",
            16 => $"mov word [{addr}], {source}",
            32 => $"mov dword [{addr}], {source}",
            _ => $"mov [{addr}], {source}",
        };
    }
}

internal static class AddressRefEncoding
{
    internal static string EncodeString(string value) => "addrstr:" + Uri.EscapeDataString(value);

    internal static bool IsStringRef(IrValue? value)
        => value?.Name?.StartsWith("addrstr:", StringComparison.Ordinal) == true;

    internal static string DecodeString(IrValue value)
        => Uri.UnescapeDataString(value.Name![8..]);
}
