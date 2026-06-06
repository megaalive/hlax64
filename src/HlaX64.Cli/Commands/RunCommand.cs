using System.ComponentModel;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using Spectre.Console.Cli;
using System.Diagnostics;

namespace HlaX64.Cli.Commands;

public sealed class RunCommand : Command<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings, IVerificationCliOptions
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Warn when a literal array index may be out of bounds")]
        [CommandOption("-Wbounds|--warn-bounds")]
        public bool WarnBounds { get; set; }

        [CommandOption("-Wdefinite|--warn-definite")]
        public bool WarnDefinite { get; set; }

        [CommandOption("-Wunreachable|--warn-unreachable")]
        public bool WarnUnreachable { get; set; }

        [CommandOption("-Wliveness|--warn-liveness")]
        public bool WarnLiveness { get; set; }

        [CommandOption("-Wverify|--warn-verify")]
        public bool WarnVerify { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            Console.Error.WriteLine($"Error: Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceFile = Path.GetFullPath(settings.Source);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var sourceDir = Path.GetDirectoryName(sourceFile)!;
        var outputDir = Path.GetFullPath(Path.Combine(sourceDir, "..", "build", sourceName));
        Directory.CreateDirectory(outputDir);

        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outputDir, $"{sourceName}.o");
        var exeFile = Path.Combine(outputDir, sourceName);

        try
        {
            // 1. Compile .hla64 -> NASM via pipeline
            Console.WriteLine($"Compiling {sourceFile}...");
            var sourceText = File.ReadAllText(sourceFile);
            var options = CliCompilationOptions.FromCli(
                null, null, settings.WarnBounds,
                settings.WarnDefinite, settings.WarnUnreachable, settings.WarnLiveness, settings.WarnVerify);
            var nasmCode = CompilePipeline.EmitNasm(sourceFile, sourceText, options);
            File.WriteAllText(nasmFile, nasmCode);

            // 2. Assemble NASM -> .o
            Console.WriteLine("Assembling with NASM...");
            if (!NasmTool.TryFindNasm(out var nasmPath))
            {
                Console.Error.WriteLine("Error: NASM not found. Install NASM (https://nasm.us)");
                return 1;
            }

            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError))
            {
                Console.Error.WriteLine($"Assembly error:\n{nasmError}");
                return 1;
            }

            // 3. Link .o -> executable
            Console.WriteLine("Linking...");
            if (!LinkerTool.TryLink(objFile, exeFile, out var linkError, out var requiresWslRun))
            {
                Console.Error.WriteLine($"Link error:\n{linkError}");
                return 1;
            }

            // 4. Make executable
            if (!requiresWslRun)
            {
                try
                {
                    var chmod = new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{exeFile}\"",
                        UseShellExecute = false
                    };
                    Process.Start(chmod)?.WaitForExit(2000);
                }
                catch { }
            }

            // 5. Run
            ProcessStartInfo psi;
            if (requiresWslRun)
            {
                string wslExeFile = LinkerTool.ToWslPath(exeFile);
                Console.WriteLine($"\nRunning via WSL: {wslExeFile}\n");
                Console.WriteLine("--- Program output ---");

                psi = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = wslExeFile,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
            }
            else
            {
                Console.WriteLine($"\nRunning: {exeFile}\n");
                Console.WriteLine("--- Program output ---");

                psi = new ProcessStartInfo
                {
                    FileName = exeFile,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
            }

            using var process = Process.Start(psi);
            if (process == null)
            {
                Console.Error.WriteLine("Failed to start process.");
                return 1;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            Console.Write(stdout);
            if (!string.IsNullOrEmpty(stderr))
                Console.Error.Write(stderr);

            Console.WriteLine($"\n--- Program exited with code {process.ExitCode} ---");
            return process.ExitCode;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}