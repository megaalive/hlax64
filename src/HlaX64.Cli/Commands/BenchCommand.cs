using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using HlaX64.Cli.Toolchain;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class BenchCommand : Command<BenchCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to a benchmark JSON manifest or directory of .hla64 files")]
        [CommandArgument(0, "[path]")]
        public string? Path { get; set; }

        [Description("Output results as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }

        [Description("Number of warmup iterations")]
        [CommandOption("--warmup")]
        [DefaultValue(3)]
        public int Warmup { get; set; }

        [Description("Number of measured iterations")]
        [CommandOption("--iterations")]
        [DefaultValue(10)]
        public int Iterations { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var targetPath = settings.Path ?? Path.Combine(Directory.GetCurrentDirectory(), "examples");

        if (File.Exists(targetPath))
        {
            if (targetPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                return RunManifest(targetPath, settings);
            return RunSingleFile(targetPath, settings);
        }

        if (Directory.Exists(targetPath))
        {
            var files = Directory.GetFiles(targetPath, "*.hla64");
            if (files.Length == 0)
            {
                AnsiConsole.MarkupLine("[yellow]No .hla64 files found[/]");
                return 0;
            }
            var results = new List<BenchmarkResult>();
            foreach (var file in files)
            {
                var name = Path.GetFileNameWithoutExtension(file);
                AnsiConsole.MarkupLine($"Benchmarking [cyan]{name}[/]...");

                var compileSw = Stopwatch.StartNew();
                var (exeFile, compileOk, wslRun) = BuildHla64(file);
                compileSw.Stop();

                var result = BenchmarkBinary(name, exeFile, compileOk, wslRun, compileSw.Elapsed.TotalMilliseconds, settings.Warmup, settings.Iterations);
                results.Add(result);
                PrintResult(result, settings.Json);
            }
            if (settings.Json)
                Console.WriteLine(JsonSerializer.Serialize(results, JsonOpts));
            return 0;
        }

        AnsiConsole.MarkupLine($"[red]Path not found: {targetPath}[/]");
        return 1;
    }

    private int RunManifest(string manifestPath, Settings settings)
    {
        var json = File.ReadAllText(manifestPath);
        var manifest = JsonSerializer.Deserialize<BenchmarkManifest>(json);
        if (manifest?.Benchmarks == null || manifest.Benchmarks.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]Invalid manifest: no benchmarks found[/]");
            return 1;
        }

        var allResults = new List<BenchmarkResult>();
        foreach (var bm in manifest.Benchmarks)
        {
            var warmup = bm.Warmup ?? settings.Warmup;
            var iterations = bm.Iterations ?? settings.Iterations;
            AnsiConsole.MarkupLine($"Benchmarking [cyan]{bm.Name}[/]...");

            BenchmarkResult result;
            if (!string.IsNullOrEmpty(bm.Source))
            {
                var compileSw = Stopwatch.StartNew();
                var (exeFile, compileOk, wslRun) = BuildHla64(bm.Source);
                compileSw.Stop();
                result = BenchmarkBinary(bm.Name, exeFile, compileOk, wslRun, compileSw.Elapsed.TotalMilliseconds, warmup, iterations);
            }
            else if (!string.IsNullOrEmpty(bm.Command))
                result = BenchmarkCommand(bm.Name, bm.Command, warmup, iterations);
            else
            {
                AnsiConsole.MarkupLine($"  [red]Skipped: no source or command[/]");
                continue;
            }

            allResults.Add(result);
            PrintResult(result, settings.Json);
        }

        if (settings.Json)
            Console.WriteLine(JsonSerializer.Serialize(allResults, JsonOpts));
        return 0;
    }

    private int RunSingleFile(string file, Settings settings)
    {
        AnsiConsole.MarkupLine($"Benchmarking [cyan]{Path.GetFileNameWithoutExtension(file)}[/]...");
        var compileSw = Stopwatch.StartNew();
        var (exeFile, compileOk, wslRun) = BuildHla64(file);
        compileSw.Stop();
        var result = BenchmarkBinary(Path.GetFileNameWithoutExtension(file), exeFile, compileOk, wslRun, compileSw.Elapsed.TotalMilliseconds, settings.Warmup, settings.Iterations);
        PrintResult(result, settings.Json);
        if (settings.Json)
            Console.WriteLine(JsonSerializer.Serialize(result, JsonOpts));
        return 0;
    }

    private static void PrintResult(BenchmarkResult r, bool json)
    {
        if (json) return;
        AnsiConsole.MarkupLine($"  Mean:   [green]{r.MeanMs:F2}[/] ms");
        AnsiConsole.MarkupLine($"  Median: [yellow]{r.MedianMs:F2}[/] ms");
        AnsiConsole.MarkupLine($"  Min:    [blue]{r.MinMs:F2}[/] ms");
        AnsiConsole.MarkupLine($"  Max:    [red]{r.MaxMs:F2}[/] ms");
        AnsiConsole.MarkupLine($"  StdDev: {r.StdDevMs:F2} ms");
        AnsiConsole.MarkupLine($"  Iter:   {r.Iterations} | Warmup: {r.WarmupIterations}");
        AnsiConsole.MarkupLine($"  Compile: [cyan]{r.CompileDurationMs:F1}[/] ms | Binary: [cyan]{FormatSize(r.BinarySizeBytes)}[/]");
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        _ => $"{bytes / (1024.0 * 1024.0):F2} MB"
    };

    private static BenchmarkResult BenchmarkBinary(string name, string exeFile, bool compileOk, bool requiresWslRun, double compileDuration, int warmup, int iterations)
    {
        var times = new List<double>();
        var binarySize = compileOk && File.Exists(exeFile) ? new FileInfo(exeFile).Length : 0;

        for (int i = 0; i < warmup; i++)
            RunBinary(exeFile, requiresWslRun);

        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            RunBinary(exeFile, requiresWslRun);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        var stats = ComputeStats(times);
        return new BenchmarkResult
        {
            Name = name,
            MeanMs = stats.mean,
            MedianMs = stats.median,
            MinMs = stats.min,
            MaxMs = stats.max,
            StdDevMs = stats.stdDev,
            Iterations = iterations,
            WarmupIterations = warmup,
            CompileDurationMs = compileDuration,
            BinarySizeBytes = binarySize
        };
    }

    private static BenchmarkResult BenchmarkCommand(string name, string command, int warmup, int iterations)
    {
        var times = new List<double>();
        var parts = SplitCommand(command);
        var fileName = parts[0];
        var args = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : "";

        for (int i = 0; i < warmup; i++)
            RunCommandProcess(fileName, args);

        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            RunCommandProcess(fileName, args);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        var stats = ComputeStats(times);
        return new BenchmarkResult
        {
            Name = name,
            MeanMs = stats.mean,
            MedianMs = stats.median,
            MinMs = stats.min,
            MaxMs = stats.max,
            StdDevMs = stats.stdDev,
            Iterations = iterations,
            WarmupIterations = warmup
        };
    }

    private static (double mean, double median, double min, double max, double stdDev) ComputeStats(List<double> times)
    {
        if (times.Count == 0) return (0, 0, 0, 0, 0);
        var mean = times.Average();
        var sorted = times.OrderBy(t => t).ToList();
        var median = sorted.Count % 2 == 0
            ? (sorted[sorted.Count / 2 - 1] + sorted[sorted.Count / 2]) / 2.0
            : sorted[sorted.Count / 2];
        var min = times.Min();
        var max = times.Max();
        var stdDev = Math.Sqrt(times.Sum(t => (t - mean) * (t - mean)) / times.Count);
        return (mean, median, min, max, stdDev);
    }

    private static (string exePath, bool ok, bool requiresWslRun) BuildHla64(string sourceFile)
    {
        try
        {
            sourceFile = Path.GetFullPath(sourceFile);
            var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
            var sourceDir = Path.GetDirectoryName(sourceFile)!;
            var outputDir = Path.GetFullPath(Path.Combine(sourceDir, "..", "build", sourceName));
            Directory.CreateDirectory(outputDir);

            var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
            var objFile = Path.Combine(outputDir, $"{sourceName}.o");
            var exeFile = Path.Combine(outputDir, sourceName);

            var sourceText = File.ReadAllText(sourceFile);
            var nasmCode = CompilePipeline.EmitNasm(sourceFile, sourceText);
            File.WriteAllText(nasmFile, nasmCode);

            if (!NasmTool.TryFindNasm(out var nasmPath)) return (exeFile, false, false);
            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out _)) return (exeFile, false, false);

            if (!LinkerTool.TryLink(objFile, exeFile, out _, out var wslRun)) return (exeFile, false, false);
            return (exeFile, true, wslRun);
        }
        catch
        {
            return ("", false, false);
        }
    }

    private static void RunBinary(string exeFile, bool requiresWslRun)
    {
        try
        {
            ProcessStartInfo psi;
            if (requiresWslRun)
            {
                psi = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = LinkerTool.ToWslPath(exeFile),
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = exeFile,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
            }

            using var process = Process.Start(psi);
            process?.WaitForExit(30000);
        }
        catch { }
    }

    private static void RunCommandProcess(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(30000);
        }
        catch { }
    }

    private static string[] SplitCommand(string command)
    {
        var args = new List<string>();
        var current = "";
        var inQuotes = false;
        foreach (var c in command)
        {
            if (c == '"') { inQuotes = !inQuotes; continue; }
            if (c == ' ' && !inQuotes)
            {
                if (current.Length > 0) { args.Add(current); current = ""; }
            }
            else current += c;
        }
        if (current.Length > 0) args.Add(current);
        return args.ToArray();
    }
}

public sealed class BenchmarkResult
{
    public string Name { get; set; } = "";
    public double MeanMs { get; set; }
    public double MedianMs { get; set; }
    public double MinMs { get; set; }
    public double MaxMs { get; set; }
    public double StdDevMs { get; set; }
    public int Iterations { get; set; }
    public int WarmupIterations { get; set; }
    public double CompileDurationMs { get; set; }
    public long BinarySizeBytes { get; set; }
}

public sealed class BenchmarkManifest
{
    [JsonPropertyName("benchmarks")]
    public List<BenchmarkEntry> Benchmarks { get; set; } = [];
}

public sealed class BenchmarkEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("command")]
    public string? Command { get; set; }

    [JsonPropertyName("warmup")]
    public int? Warmup { get; set; }

    [JsonPropertyName("iterations")]
    public int? Iterations { get; set; }

    [JsonPropertyName("expectedExitCode")]
    public int? ExpectedExitCode { get; set; }
}
