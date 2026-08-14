namespace HlaX64.Compiler.Abi;

internal static class StackMemOperandHelper
{
    internal static bool IsBareMemory(string operand) =>
        operand.Length >= 3 && operand[0] == '[' && operand[^1] == ']';

    internal static string EmitMove(string destination, string source, int sizeBits = 64)
    {
        if (IsBareMemory(destination) && IsBareMemory(source))
        {
            var scratch = ScratchReg(sizeBits);
            return $"mov {scratch}, {FormatSizedMem(source, sizeBits)}\n    mov {FormatSizedMem(destination, sizeBits)}, {scratch}";
        }
        if (IsBareMemory(destination))
            return $"mov {FormatSizedMem(destination, sizeBits)}, {source}";
        if (IsBareMemory(source))
            return $"mov {destination}, {FormatSizedMem(source, sizeBits)}";
        return $"mov {destination}, {source}";
    }

    internal static string EmitBinary(string mnemonic, string destination, string source, int sizeBits = 64)
    {
        if (IsBareMemory(destination) && IsBareMemory(source))
        {
            var scratch = ScratchReg(sizeBits);
            return $"mov {scratch}, {FormatSizedMem(source, sizeBits)}\n    {mnemonic} {FormatSizedMem(destination, sizeBits)}, {scratch}";
        }
        if (IsBareMemory(source))
            return $"{mnemonic} {destination}, {FormatSizedMem(source, sizeBits)}";
        if (IsBareMemory(destination))
            return $"{mnemonic} {FormatSizedMem(destination, sizeBits)}, {source}";
        return $"{mnemonic} {destination}, {source}";
    }

    internal static string EmitCompare(string left, string right, int sizeBits = 64)
    {
        if (IsBareMemory(left) && IsBareMemory(right))
        {
            var scratch = ScratchReg(sizeBits);
            return $"mov {scratch}, {FormatSizedMem(left, sizeBits)}\n    cmp {scratch}, {FormatSizedMem(right, sizeBits)}";
        }
        if (IsBareMemory(left))
            left = FormatSizedMem(left, sizeBits);
        if (IsBareMemory(right))
            right = FormatSizedMem(right, sizeBits);
        return $"cmp {left}, {right}";
    }

    internal static string FormatSizedMem(string mem, int sizeBits = 64) =>
        sizeBits switch
        {
            8 => $"byte {mem}",
            16 => $"word {mem}",
            32 => $"dword {mem}",
            _ => $"qword {mem}",
        };

    private static string ScratchReg(int sizeBits) => sizeBits switch
    {
        8 => "al",
        16 => "ax",
        32 => "eax",
        _ => "rax",
    };
}
