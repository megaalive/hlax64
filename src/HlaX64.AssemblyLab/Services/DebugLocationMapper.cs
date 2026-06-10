using System.Globalization;
using System.Text.RegularExpressions;
using HlaX64.Compiler.Debug;
using HlaX64.DebugAdapter;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Maps instruction pointer addresses to source/NASM lines using objdump output.</summary>
public static partial class DebugLocationMapper
{
    public sealed record DebugLocation(
        int? SourceLine,
        int? NasmLine,
        string? Instruction,
        bool InMainModule = true,
        bool IsRuntimeCode = false,
        bool IsProgramShutdown = false);

    public static DebugLocation MapRip(
        ulong rip,
        string? disasmText,
        SourceMapDocument? sourceMap,
        string? nasmText,
        string? binaryPath = null,
        string? nasmPath = null)
    {
        var instruction = FindInstructionText(disasmText, rip);
        var inMainModule = string.IsNullOrWhiteSpace(binaryPath)
                           || string.IsNullOrWhiteSpace(nasmPath)
                           || PeDebugAddressMap.IsAddressInMainModule(rip, binaryPath, nasmPath);

        if (!inMainModule)
        {
            return new DebugLocation(
                null,
                null,
                instruction ?? $"0x{rip:x}",
                InMainModule: false,
                IsProgramShutdown: true);
        }

        if (!string.IsNullOrWhiteSpace(binaryPath)
            && !string.IsNullOrWhiteSpace(nasmPath)
            && File.Exists(binaryPath)
            && File.Exists(nasmPath))
        {
            var maps = PeDebugAddressMap.GetOrBuild(binaryPath, nasmPath, sourceMap);
            var isRuntimeCode = !PeDebugAddressMap.IsUserCodeAddress(rip, maps, binaryPath, nasmPath);
            if (isRuntimeCode)
            {
                return new DebugLocation(
                    null,
                    null,
                    instruction,
                    InMainModule: true,
                    IsRuntimeCode: true);
            }

            var sourceLine = PeDebugAddressMap.LookupSourceLine(rip, maps.SourceByAddress);
            var nasmLine = PeDebugAddressMap.LookupNasmLine(rip, maps.NasmByAddress);

            if (sourceLine != null || nasmLine != null)
            {
                return new DebugLocation(
                    sourceLine,
                    nasmLine,
                    instruction,
                    InMainModule: true,
                    IsRuntimeCode: isRuntimeCode);
            }
        }

        return MapRipFromDisasmIndex(rip, disasmText, sourceMap, nasmText, nasmPath)
            with { Instruction = instruction ?? FindInstructionText(disasmText, rip) };
    }

    private static DebugLocation MapRipFromDisasmIndex(
        ulong rip,
        string? disasmText,
        SourceMapDocument? sourceMap,
        string? nasmText,
        string? nasmPath = null)
    {
        var addresses = ParseInstructionAddresses(disasmText);
        if (addresses.Count == 0)
            return new DebugLocation(null, null, null);

        var instructionIndex = 0;
        for (var i = 0; i < addresses.Count; i++)
        {
            if (addresses[i].Address <= rip)
                instructionIndex = i;
            else
                break;
        }

        var (_, text) = addresses[instructionIndex];
        var nasmLine = MapInstructionIndexToNasmLine(instructionIndex, nasmText);
        int? resolvedSourceLine = null;
        if (nasmLine != null && !string.IsNullOrWhiteSpace(nasmPath) && File.Exists(nasmPath))
        {
            var sourceLine = PeDebugAddressMap.ResolveSourceLineForNasm(sourceMap, nasmPath, nasmLine.Value);
            if (sourceLine > 0)
                resolvedSourceLine = sourceLine;
        }

        return new DebugLocation(resolvedSourceLine, nasmLine, text);
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

    private static string? FindInstructionText(string? disasmText, ulong rip)
    {
        foreach (var (address, text) in ParseInstructionAddresses(disasmText))
        {
            if (address == rip)
                return text;
        }

        return null;
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
        for (var i = 0; i < lines.Length; i++)
        {
            if (NasmLineClassifier.IsInstructionLine(lines[i]))
                codeLines.Add(i + 1);
        }

        if (codeLines.Count == 0)
            return null;

        var index = Math.Clamp(instructionIndex, 0, codeLines.Count - 1);
        return codeLines[index];
    }

    [GeneratedRegex(@"^(?<addr>[0-9a-fA-F]{2,16}):\s+(?<rest>.+)$")]
    private static partial Regex InstructionAddress();
}
