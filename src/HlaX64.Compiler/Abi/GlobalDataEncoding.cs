using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class GlobalDataEncoding
{
    internal static string Encode(string name) => $"global:{name}";

    internal static bool IsGlobalRef(IrValue? value)
        => value?.Name?.StartsWith("global:", StringComparison.Ordinal) == true;

    internal static string DecodeName(IrValue value) => value.Name![7..];

    internal static string FormatMem(string name, int sizeBits = 64) => sizeBits switch
    {
        8 => $"byte [{name}]",
        16 => $"word [{name}]",
        32 => $"dword [{name}]",
        _ => $"[{name}]"
    };
}
