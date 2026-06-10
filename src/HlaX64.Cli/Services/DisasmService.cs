using System.Diagnostics;
using System.Text;
using HlaX64.Compiler.Debug;
using HlaX64.DebugAdapter;

namespace HlaX64.Cli.Services;

public static class DisasmService
{
    public static string FormatNasmWithSourceMap(string nasmText, SourceMapDocument? map)
    {
        var lines = nasmText.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var entry = map?.LookupByNasmLine(i + 1);
            if (entry?.SourceLine != null
                && entry.NasmLine == i + 1
                && PeDebugAddressMap.IsTrustedSourceMapNasmLine(map, i + 1))
                sb.AppendLine($"; src:{entry.SourceLine,3} | {line}");
            else
                sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatBinaryDisasm(string binaryPath, SourceMapDocument? map, string? nasmPath = null)
    {
        if (!File.Exists(binaryPath))
            return $"Binary not found: {binaryPath}";

        if (!TryObjdump(binaryPath, out var objdumpLines))
            return "objdump not available (install binutils or LLVM objdump on PATH).";

        IReadOnlyDictionary<ulong, int>? sourceByAddress = null;
        if (map != null && !string.IsNullOrWhiteSpace(nasmPath) && File.Exists(nasmPath))
            sourceByAddress = PeDebugAddressMap.GetOrBuild(binaryPath, nasmPath, map).SourceByAddress;

        var sb = new StringBuilder();
        foreach (var line in objdumpLines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (sourceByAddress != null
                && TryParseObjdumpAddress(line, out var addr)
                && PeDebugAddressMap.LookupSourceLine((ulong)addr, sourceByAddress) is int srcLine)
            {
                sb.AppendLine($"; src:{srcLine,3} | {line.TrimEnd()}");
                continue;
            }

            sb.AppendLine(line.TrimEnd());
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatDisasm(string? nasmText, SourceMapDocument? map, string? binaryPath, string? nasmPath = null)
    {
        if (!string.IsNullOrEmpty(binaryPath) && File.Exists(binaryPath))
        {
            var binary = FormatBinaryDisasm(binaryPath, map, nasmPath);
            if (!binary.StartsWith("objdump not available", StringComparison.OrdinalIgnoreCase))
                return binary;
        }

        if (!string.IsNullOrEmpty(nasmText))
        {
            var listing = FormatNasmWithSourceMap(nasmText, map);
            return string.IsNullOrEmpty(listing)
                ? "; Build or compile to view disassembly."
                : "; NASM listing — build linked binary for machine disassembly (objdump)." + Environment.NewLine + listing;
        }

        return "Build or compile to view disassembly (NASM listing or linked binary).";
    }

    private static bool TryParseObjdumpAddress(string line, out long address)
    {
        address = 0;
        var parts = line.TrimStart().Split(':', 2);
        if (parts.Length < 2)
            return false;
        return long.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out address);
    }

    private static bool TryObjdump(string binary, out List<string> lines)
    {
        lines = [];
        var tools = OperatingSystem.IsWindows()
            ? new[] { "llvm-objdump", "objdump" }
            : new[] { "objdump", "llvm-objdump" };

        foreach (var objdump in tools)
        {
            var resolved = HlaX64.Cli.Toolchain.LinkerTool.ResolveToolExecutable(objdump);
            if (resolved == null)
                continue;

            try
            {
                using var p = Process.Start(new ProcessStartInfo
                {
                    FileName = resolved,
                    Arguments = $"-d -M intel \"{binary}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (p == null)
                    continue;

                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                if (p.ExitCode != 0)
                    continue;

                lines = output
                    .Replace("\r\n", "\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();
                if (lines.Count > 0)
                    return true;
            }
            catch
            {
                // try next objdump
            }
        }

        return false;
    }
}
