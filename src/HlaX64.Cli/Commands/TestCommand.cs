using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

/// <summary>
/// Runs all test manifests in a directory. Each manifest is a JSON file
/// describing a .hla64 program and the expected stdout / exit code.
/// </summary>
/// <remarks>
/// Fase 9 deliverable. With --compile-only, the runner produces NASM and
/// asserts the manifest's source compiled without invoking the
/// assembler / linker / native runner. Without the flag, the runner
/// builds and executes the program and compares actual output to
/// expected values.
/// </remarks>
public sealed class TestCommand : Command<TestCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Directory containing test manifest JSON files (recursively scanned)")]
        [CommandArgument(0, "[directory]")]
        public string Directory { get; set; } = "tests/samples";

        [Description("Build base directory (default: build/tests/<run-id>)")]
        [CommandOption("-o|--output")]
        public string? OutputDir { get; set; }

        [Description("Only compile to NASM; do not assemble, link, or run")]
        [CommandOption("--compile-only")]
        public bool CompileOnly { get; set; }

        [Description("Verbose per-test output")]
        [CommandOption("-v|--verbose")]
        public bool Verbose { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var directory = Path.GetFullPath(settings.Directory);
        if (!Directory.Exists(directory))
        {
            Console.Error.WriteLine($"Error: Test directory '{directory}' not found.");
            return 1;
        }

        // Build base dir
        var buildBase = settings.OutputDir != null
            ? Path.GetFullPath(settings.OutputDir)
            : Path.Combine(Path.GetTempPath(), "hlax64_tests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(buildBase);

        // Toolchain for full execution
        string? nasmPath = null;
        TestRunner.LinkerRunner? linkerRunner = null;
        TestRunner.BinaryExecutor? binaryExecutor = null;

        if (!settings.CompileOnly)
        {
            if (NasmTool.TryFindNasm(out var found))
                nasmPath = found;
            else
                Console.Error.WriteLine("Warning: NASM not found. Use --compile-only or install NASM.");

            // Refresh PATH from registry so recently-installed toolchains are visible
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var machine = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.Machine) ?? "";
                var user    = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
                Environment.SetEnvironmentVariable("Path", machine + ";" + user + ";" + Environment.GetEnvironmentVariable("Path"));
            }

            // Detect linker and build delegates
            if (LinkerTool.TryFindLinker(out var linkerPath, out var linkerDisplay, out var subCommand))
            {
                bool requiresWsl = linkerPath == "wsl";

                linkerRunner = (objFile, exeFile) =>
                {
                    if (LinkerTool.TryLink(objFile, exeFile, out var error, out var wsl))
                        return (true, "", wsl);
                    return (false, error, wsl);
                };

                binaryExecutor = (exeFile, timeoutMs) =>
                {
                    try
                    {
                        string fileName;
                        string args;
                        if (requiresWsl)
                        {
                            fileName = "wsl";
                            args = LinkerTool.ToWslPath(exeFile);
                        }
                        else
                        {
                            fileName = exeFile;
                            args = "";
                        }

                        using var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = fileName,
                            Arguments = args,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        if (process == null)
                            return (-1, "", "Failed to start process");

                        if (!process.WaitForExit(timeoutMs))
                        {
                            process.Kill();
                            return (-1, "", "Process timed out");
                        }

                        var stdout = process.StandardOutput.ReadToEnd();
                        return (process.ExitCode, stdout, "");
                    }
                    catch (Exception ex)
                    {
                        return (-1, "", ex.Message);
                    }
                };
            }
            else
            {
                Console.Error.WriteLine("Warning: No linker found for execution tests. Use --compile-only or install gcc.");
            }
        }

        // Manifests
        var manifests = TestManifest.LoadAll(directory);
        if (manifests.Count == 0)
        {
            Console.Error.WriteLine($"Error: No test manifests found in '{directory}'.");
            return 1;
        }

        Console.WriteLine($"Running {manifests.Count} test(s) from {directory}");
        if (settings.CompileOnly) Console.WriteLine("  (compile-only mode)");
        Console.WriteLine();

        // Per-test compile function
        Func<string, string> compileFunc = source =>
        {
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var program = parser.Parse();
            var emitter = new NasmEmitter();
            return emitter.Emit(program);
        };

        var runner = new TestRunner(
            compileFunc: compileFunc,
            nasmPath: nasmPath,
            skipExecution: settings.CompileOnly,
            linkerRunner: linkerRunner,
            binaryExecutor: binaryExecutor);

        int passed = 0;
        int failed = 0;
        var sw = Stopwatch.StartNew();

        foreach (var manifest in manifests)
        {
            if (cancellation.IsCancellationRequested) break;

            var buildDir = Path.Combine(buildBase, manifest.Name);
            var result = runner.RunTest(manifest, buildDir);

            // Pretty output
            var status = result.Passed ? "[green]PASS[/]" : "[red]FAIL[/]";
            var line = $"{status}  {result.Name,-20} {result.Duration.TotalMilliseconds,6:F0}ms";
            if (!result.Passed && !string.IsNullOrEmpty(result.ErrorMessage))
                line += $"  — {result.ErrorMessage}";
            Console.WriteLine(line);

            if (settings.Verbose && result.Passed && !string.IsNullOrEmpty(result.ActualStdout))
            {
                foreach (var ln in result.ActualStdout.Split('\n'))
                    Console.WriteLine($"        | {ln.TrimEnd('\r')}");
            }

            if (result.Passed) passed++; else failed++;
        }

        sw.Stop();
        Console.WriteLine();
        Console.WriteLine($"Results: {passed} passed, {failed} failed, {manifests.Count} total  ({sw.Elapsed.TotalSeconds:F2}s)");

        return failed == 0 ? 0 : 1;
    }
}
