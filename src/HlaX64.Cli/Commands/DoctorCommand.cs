using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HlaX64.Compiler;
using HlaX64.Cli.Toolchain;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class DoctorCommand : Command<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Output results as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var results = new List<CheckResult>();

        // .NET version
        results.Add(CheckDotNet());

        // Platform
        results.Add(CheckPlatform());

        // NASM
        results.Add(CheckNasm());

        // Linker
        results.Add(CheckLinker());

        // WSL (Windows only)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            results.Add(CheckWsl());

        // Runtime files
        results.Add(CheckRuntimeFiles());

        if (settings.Json)
        {
            Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
                new { version = Compilation.GetVersion(), checks = results },
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }

        AnsiConsole.MarkupLine($"[bold]HlaX64 Doctor[/] — v{Compilation.GetVersion()}");
        AnsiConsole.MarkupLine("");

        foreach (var r in results)
        {
            var icon = r.Passed ? "[green]\u2714[/]" : "[red]\u2718[/]";
            AnsiConsole.MarkupLine($"  {icon} {r.Name}: {r.Message}");
        }

        var allPassed = results.All(r => r.Passed);
        AnsiConsole.MarkupLine(allPassed ? "\n[green]All checks passed.[/]" : "\n[yellow]Some checks failed. See details above.[/]");

        return allPassed ? 0 : 1;
    }

    private static CheckResult CheckDotNet()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (process == null) return Fail(".NET SDK", "Could not start dotnet process");
            process.WaitForExit(5000);
            var version = process.StandardOutput.ReadToEnd().Trim();
            return Pass(".NET SDK", version);
        }
        catch (Exception ex)
        {
            return Fail(".NET SDK", ex.Message);
        }
    }

    private static CheckResult CheckPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return Pass("Platform", "Linux " + RuntimeInformation.OSDescription);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Pass("Platform", "Windows " + RuntimeInformation.OSDescription);
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Pass("Platform", "macOS " + RuntimeInformation.OSDescription);
        return Pass("Platform", RuntimeInformation.OSDescription);
    }

    private static CheckResult CheckNasm()
    {
        if (NasmTool.TryFindNasm(out var path))
        {
            var (fileName, prefixArgs) = NasmTool.SplitNasmInvocation(path);
            var versionArgs = string.IsNullOrEmpty(prefixArgs) ? "--version" : $"{prefixArgs} --version";
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = versionArgs,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                if (process != null)
                {
                    process.WaitForExit(3000);
                    var ver = process.StandardOutput.ReadToEnd().Trim().Split('\n')[0];
                    return Pass("NASM", ver);
                }
            }
            catch { }
            return Pass("NASM", $"Found at {path}");
        }
        return Fail("NASM", "Not found. Install from https://nasm.us");
    }

    private static CheckResult CheckLinker()
    {
        if (LinkerTool.TryFindLinker(out var path, out var displayName, out _))
            return Pass("Linker (Linux)", $"{displayName} at {path}");
        if (LinkerTool.TryFindWindowsLinker(out var winPath, out var winDisplay, out _))
            return Pass("Linker (Windows)", $"{winDisplay} at {winPath}");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Check if WSL has gcc
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = "gcc --version",
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                });
                if (process != null)
                {
                    process.WaitForExit(5000);
                    if (process.ExitCode == 0)
                        return Pass("Linker (via WSL)", "gcc available in WSL");
                }
            }
            catch { }
        }

        return Fail("Linker", "No supported linker found. See docs for setup instructions.");
    }

    private static CheckResult CheckWsl()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "--status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process == null) return Fail("WSL", "Not available");
            process.WaitForExit(5000);
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrEmpty(output))
                output = process.StandardError.ReadToEnd().Trim();
            var lines = output.Split('\n', StringSplitOptions.TrimEntries);
            var status = lines.Length > 0 ? lines[0] : "Available";
            return Pass("WSL", status);
        }
        catch
        {
            return Fail("WSL", "Not available. Install WSL2 for Linux x64 target support.");
        }
    }

    private static CheckResult CheckRuntimeFiles()
    {
        var runtimeDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "HlaX64.Runtime");
        var includeDir = Path.Combine(runtimeDir, "include");
        if (Directory.Exists(includeDir) && Directory.GetFiles(includeDir, "*.hhf").Length > 0)
            return Pass("Runtime files", $"Found in {runtimeDir}");
        return Fail("Runtime files", $"Not found at {runtimeDir}");
    }

    private static CheckResult Pass(string name, string message) => new() { Name = name, Passed = true, Message = message };
    private static CheckResult Fail(string name, string message) => new() { Name = name, Passed = false, Message = message };

    private sealed class CheckResult
    {
        public string Name { get; set; } = "";
        public bool Passed { get; set; }
        public string Message { get; set; } = "";
    }
}
