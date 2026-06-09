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
            32 => $"mov {RegisterForBits(destination, 32)}, dword [{addr}]",
            _ => $"mov {destination}, qword [{addr}]",
        };
    }

    internal static string EmitStore(Parsed mem, string source)
    {
        var addr = FormatAddress(mem);
        return mem.SizeBits switch
        {
            8 => $"mov byte [{addr}], {RegisterForBits(source, 8)}",
            16 => $"mov word [{addr}], {RegisterForBits(source, 16)}",
            32 => $"mov dword [{addr}], {RegisterForBits(source, 32)}",
            _ => $"mov qword [{addr}], {source}",
        };
    }

    private static string RegisterForBits(string operand, int bits)
    {
        return bits switch
        {
            8 => Register8(operand),
            16 => Register16(operand),
            32 => Register32(operand),
            _ => operand
        };
    }

    private static string Register8(string operand)
    {
        return operand.ToLowerInvariant() switch
        {
            "rax" or "eax" => "al",
            "rbx" or "ebx" => "bl",
            "rcx" or "ecx" => "cl",
            "rdx" or "edx" => "dl",
            "rsi" or "esi" => "sil",
            "rdi" or "edi" => "dil",
            "rbp" or "ebp" => "bpl",
            "rsp" or "esp" => "spl",
            "r8" or "r8d" => "r8b",
            "r9" or "r9d" => "r9b",
            "r10" or "r10d" => "r10b",
            "r11" or "r11d" => "r11b",
            "r12" or "r12d" => "r12b",
            "r13" or "r13d" => "r13b",
            "r14" or "r14d" => "r14b",
            "r15" or "r15d" => "r15b",
            _ => operand
        };
    }

    private static string Register16(string operand)
    {
        return operand.ToLowerInvariant() switch
        {
            "rax" or "eax" => "ax",
            "rbx" or "ebx" => "bx",
            "rcx" or "ecx" => "cx",
            "rdx" or "edx" => "dx",
            "rsi" or "esi" => "si",
            "rdi" or "edi" => "di",
            "rbp" or "ebp" => "bp",
            "rsp" or "esp" => "sp",
            "r8" or "r8d" => "r8w",
            "r9" or "r9d" => "r9w",
            "r10" or "r10d" => "r10w",
            "r11" or "r11d" => "r11w",
            "r12" or "r12d" => "r12w",
            "r13" or "r13d" => "r13w",
            "r14" or "r14d" => "r14w",
            "r15" or "r15d" => "r15w",
            _ => operand
        };
    }

    private static string Register32(string operand)
    {
        return operand.ToLowerInvariant() switch
        {
            "rax" => "eax",
            "rbx" => "ebx",
            "rcx" => "ecx",
            "rdx" => "edx",
            "rsi" => "esi",
            "rdi" => "edi",
            "rbp" => "ebp",
            "rsp" => "esp",
            "r8" => "r8d",
            "r9" => "r9d",
            "r10" => "r10d",
            "r11" => "r11d",
            "r12" => "r12d",
            "r13" => "r13d",
            "r14" => "r14d",
            "r15" => "r15d",
            _ => operand
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
