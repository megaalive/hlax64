using System.Diagnostics;
using System.Text;
using HlaX64.Compiler.Debug;

namespace HlaX64.Cli.Services;

public static class DisasmService
{
    public static string FormatNasmWithSourceMap(string nasmText, SourceMapDocument? map)
    {
        var lines = nasmText.Replace("\r\n", "\n").Split('\n');
        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var entry = map?.LookupByNasmLine(i + 1);
            if (entry?.SourceLine != null)
                sb.AppendLine($"{i + 1,4} | src:{entry.SourceLine,3} | {lines[i]}");
            else
                sb.AppendLine($"{i + 1,4} | {lines[i]}");
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatBinaryDisasm(string binaryPath, SourceMapDocument? map)
    {
        if (!File.Exists(binaryPath))
            return $"Binary not found: {binaryPath}";

        if (!TryObjdump(binaryPath, out var objdumpLines))
            return "objdump not available (install binutils or LLVM objdump on PATH).";

        var sb = new StringBuilder();
        foreach (var line in objdumpLines)
        {
            if (map != null && TryParseObjdumpAddress(line, out _))
            {
                var near = map.Entries.FirstOrDefault(e => e.NasmLine != null);
                if (near != null)
                {
                    sb.AppendLine($"src:{near.SourceLine,3} | {line}");
                    continue;
                }
            }

            sb.AppendLine(line);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatDisasm(string? nasmText, SourceMapDocument? map, string? binaryPath)
    {
        if (!string.IsNullOrEmpty(binaryPath) && File.Exists(binaryPath))
        {
            var binary = FormatBinaryDisasm(binaryPath, map);
            if (!binary.StartsWith("objdump not available", StringComparison.OrdinalIgnoreCase))
                return binary;
        }

        if (!string.IsNullOrEmpty(nasmText))
            return FormatNasmWithSourceMap(nasmText, map);

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
        foreach (var objdump in new[] { "objdump", "llvm-objdump" })
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
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                if (p == null)
                    continue;

                var output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                if (p.ExitCode != 0)
                    continue;

                lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
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
