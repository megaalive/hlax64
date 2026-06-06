using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
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

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var targetDir = settings.Path ?? Path.Combine(Directory.GetCurrentDirectory(), "examples");
        if (!Directory.Exists(targetDir))
        {
            AnsiConsole.MarkupLine($"[red]Directory not found: {targetDir}[/]");
            return 1;
        }

        var files = Directory.GetFiles(targetDir, "*.hla64");
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

            var result = BenchmarkFile(file, settings.Warmup, settings.Iterations);
            results.Add(result);

            if (!settings.Json)
            {
                AnsiConsole.MarkupLine($"  Mean: [green]{result.MeanMs:F2}[/] ms");
                AnsiConsole.MarkupLine($"  Min:  [blue]{result.MinMs:F2}[/] ms");
                AnsiConsole.MarkupLine($"  Max:  [red]{result.MaxMs:F2}[/] ms");
                AnsiConsole.MarkupLine($"  StdDev: {result.StdDevMs:F2} ms");
            }
        }

        if (settings.Json)
        {
            Console.WriteLine(JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        }

        return 0;
    }

    private static BenchmarkResult BenchmarkFile(string file, int warmup, int iterations)
    {
        var name = Path.GetFileNameWithoutExtension(file);
        var times = new List<double>();

        // Warmup
        for (int i = 0; i < warmup; i++)
        {
            var sw = Stopwatch.StartNew();
            CompileAndRun(file);
            sw.Stop();
        }

        // Measure
        for (int i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            CompileAndRun(file);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }

        var mean = times.Average();
        var min = times.Min();
        var max = times.Max();
        var stdDev = Math.Sqrt(times.Sum(t => (t - mean) * (t - mean)) / times.Count);

        return new BenchmarkResult
        {
            Name = name,
            MeanMs = mean,
            MinMs = min,
            MaxMs = max,
            StdDevMs = stdDev,
            Iterations = iterations
        };
    }

    private static void CompileAndRun(string file)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project src/HlaX64.Cli -- run \"{file}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = GetRepoRoot()
            });
            process?.WaitForExit(30000);
        }
        catch
        {
            // ignore benchmark failures
        }
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HlaX64.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        return Directory.GetCurrentDirectory();
    }

    private sealed class BenchmarkResult
    {
        public string Name { get; set; } = "";
        public double MeanMs { get; set; }
        public double MinMs { get; set; }
        public double MaxMs { get; set; }
        public double StdDevMs { get; set; }
        public int Iterations { get; set; }
    }
}
