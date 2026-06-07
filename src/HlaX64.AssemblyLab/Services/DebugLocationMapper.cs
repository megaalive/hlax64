using System.Globalization;
using System.Text.RegularExpressions;
using HlaX64.Compiler.Debug;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Maps instruction pointer addresses to source/NASM lines using objdump output.</summary>
public static partial class DebugLocationMapper
{
    public sealed record DebugLocation(int? SourceLine, int? NasmLine, string? Instruction);

    public static DebugLocation MapRip(ulong rip, string? disasmText, SourceMapDocument? sourceMap, string? nasmText)
    {
        var addresses = ParseInstructionAddresses(disasmText);
        if (addresses.Count == 0)
            return new DebugLocation(null, null, null);

        var instructionIndex = 0;
        for (int i = 0; i < addresses.Count; i++)
        {
            if (addresses[i].Address <= rip)
                instructionIndex = i;
            else
                break;
        }

        var (_, text) = addresses[instructionIndex];
        var nasmLine = MapInstructionIndexToNasmLine(instructionIndex, nasmText);
        var sourceLine = nasmLine != null
            ? sourceMap?.LookupByNasmLine(nasmLine.Value)?.SourceLine
            : null;

        return new DebugLocation(sourceLine, nasmLine, text);
    }

    public static ulong? ParseAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var addr)
            ? addr
            : null;
    }

    private static List<(ulong Address, string Text)> ParseInstructionAddresses(string? disasmText)
    {
        var list = new List<(ulong, string)>();
        if (string.IsNullOrWhiteSpace(disasmText))
            return list;

        foreach (var line in disasmText.Replace("\r\n", "\n").Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(';'))
            {
                var idx = trimmed.IndexOf('|');
                if (idx >= 0)
                    trimmed = trimmed[(idx + 1)..].Trim();
            }

            var match = InstructionAddress().Match(trimmed);
            if (!match.Success)
                continue;

            if (!ulong.TryParse(match.Groups["addr"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var addr))
                continue;

            list.Add((addr, trimmed));
        }

        return list;
    }

    private static int? MapInstructionIndexToNasmLine(int instructionIndex, string? nasmText)
    {
        if (string.IsNullOrWhiteSpace(nasmText))
            return null;

        var codeLines = new List<int>();
        var lines = nasmText.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (line.Length == 0 || line.StartsWith(';'))
                continue;
            if (IsNasmDirective(line))
                continue;
            codeLines.Add(i + 1);
        }

        if (codeLines.Count == 0)
            return null;

        var index = Math.Clamp(instructionIndex, 0, codeLines.Count - 1);
        return codeLines[index];
    }

    private static bool IsNasmDirective(string line)
    {
        if (line.EndsWith(':'))
            return false;

        var first = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return first.Equals("bits", StringComparison.OrdinalIgnoreCase)
               || first.Equals("default", StringComparison.OrdinalIgnoreCase)
               || first.Equals("section", StringComparison.OrdinalIgnoreCase)
               || first.Equals("global", StringComparison.OrdinalIgnoreCase)
               || first.Equals("extern", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"^(?<addr>[0-9a-fA-F]{2,16}):\s+(?<rest>.+)$")]
    private static partial Regex InstructionAddress();
}
