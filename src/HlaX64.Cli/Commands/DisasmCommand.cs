using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Debug;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class DisasmCommand : Command<DisasmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<input>")]
        public string Input { get; set; } = string.Empty;

        [CommandOption("--source-map")]
        public string? SourceMapPath { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Input))
        {
            Report(settings, false, error: $"Input file '{settings.Input}' not found.");
            return 1;
        }

        SourceMapDocument? map = null;
        if (settings.SourceMapPath != null && File.Exists(settings.SourceMapPath))
            map = JsonSerializer.Deserialize<SourceMapDocument>(File.ReadAllText(settings.SourceMapPath));

        var lines = new List<object>();
        if (settings.Input.EndsWith(".nasm", StringComparison.OrdinalIgnoreCase))
        {
            var nasmLines = File.ReadAllLines(settings.Input);
            for (int i = 0; i < nasmLines.Length; i++)
            {
                var entry = map?.LookupByNasmLine(i + 1);
                lines.Add(new
                {
                    nasmLine = i + 1,
                    nasm = nasmLines[i],
                    sourceLine = entry?.SourceLine,
                    sourceColumn = entry?.SourceColumn,
                    irId = entry?.IrId
                });
            }
        }
        else if (TryObjdump(settings.Input, out var objdumpLines))
        {
            foreach (var line in objdumpLines)
            {
                var merged = new Dictionary<string, object?> { ["disasm"] = line };
                if (map != null && TryParseObjdumpAddress(line, out var addr))
                {
                    var near = map.Entries.FirstOrDefault(e => e.NasmLine != null);
                    if (near != null)
                    {
                        merged["sourceLine"] = near.SourceLine;
                        merged["irId"] = near.IrId;
                    }
                }
                lines.Add(merged);
            }
        }
        else
        {
            Report(settings, false, error: "Unsupported input (use .nasm or ELF binary with objdump on PATH).");
            return 1;
        }

        Report(settings, true, new { input = settings.Input, sourceMap = settings.SourceMapPath, lines });
        return 0;
    }

    private static bool TryParseObjdumpAddress(string line, out long address)
    {
        address = 0;
        var parts = line.TrimStart().Split(':', 2);
        if (parts.Length < 2) return false;
        return long.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out address);
    }

    private static bool TryObjdump(string binary, out List<string> lines)
    {
        lines = [];
        try
        {
            using var p = Process.Start(new ProcessStartInfo
            {
                FileName = "objdump",
                Arguments = $"-d -M intel \"{binary}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (p == null) return false;
            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(5000);
            if (p.ExitCode != 0) return false;
            lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
            return lines.Count > 0;
        }
        catch
        {
            return false;
        }
    }

    private static void Report(Settings settings, bool success, object? result = null, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                result,
                error
            });
            return;
        }

        if (!success)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        foreach (var line in doc.RootElement.GetProperty("lines").EnumerateArray())
        {
            if (line.TryGetProperty("sourceLine", out var src) && src.ValueKind != JsonValueKind.Null)
                Console.WriteLine($"{line.GetProperty("nasmLine").GetInt32(),4} | src:{src.GetInt32(),3} | {line.GetProperty("nasm").GetString()}");
            else if (line.TryGetProperty("nasm", out var nasm))
                Console.WriteLine($"{line.GetProperty("nasmLine").GetInt32(),4} | {nasm.GetString()}");
            else
                Console.WriteLine(line.GetProperty("disasm").GetString());
        }
    }
}
