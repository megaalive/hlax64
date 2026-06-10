using System.Runtime.InteropServices;

namespace HlaX64.DebugAdapter;

/// <summary>Minimal x64 decoder for Step Over call support.</summary>
public static class X64CallInstruction
{
    public static bool TryGetCallReturnRip(nint processHandle, ulong rip, out ulong returnRip)
    {
        returnRip = 0;
        if (processHandle == 0 || rip == 0)
            return false;

        var bytes = new byte[15];
        if (!Win32DebugNative.TryReadMemory(processHandle, rip, bytes))
            return false;

        var length = TryGetCallInstructionLength(bytes);
        if (length <= 0)
            return false;

        returnRip = rip + (ulong)length;
        return true;
    }

    public static int TryGetCallInstructionLength(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0)
            return 0;

        if (bytes[0] == 0xE8)
            return bytes.Length >= 5 ? 5 : 0;

        if (bytes[0] != 0xFF || bytes.Length < 2)
            return 0;

        if (((bytes[1] >> 3) & 7) != 2)
            return 0;

        return DecodeCallRmLength(bytes);
    }

    private static int DecodeCallRmLength(ReadOnlySpan<byte> bytes)
    {
        var modRm = bytes[1];
        var mod = modRm >> 6;
        var rm = modRm & 7;
        var offset = 2;

        if (mod == 3)
            return offset;

        if (rm == 4)
        {
            if (bytes.Length <= offset)
                return 0;

            var sib = bytes[offset];
            offset++;
            rm = sib & 7;
        }

        offset += mod switch
        {
            0 when rm == 5 => 4,
            1 => 1,
            2 => 4,
            _ => 0
        };

        return offset <= bytes.Length ? offset : 0;
    }
}
