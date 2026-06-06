using System.ComponentModel;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler.Options;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class BuildCommand : Command<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Output directory (default: build/)")]
        [CommandOption("-o|--output")]
        public string? OutputDir { get; set; }

        [Description("Runtime mode: inline (default) or library")]
        [CommandOption("--runtime")]
        public string? RuntimeMode { get; set; }
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
        var outputDir = settings.OutputDir ?? Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "build", sourceName);
        outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputDir);

        var options = CompilationOptions.Default;
        if (settings.RuntimeMode?.ToLowerInvariant() == "library")
            options = options with { RuntimeMode = HlaX64.Compiler.Options.RuntimeMode.Library };

        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outputDir, $"{sourceName}.o");
        var exeFile = Path.Combine(outputDir, sourceName);

        try
        {
            // 1. Compile .hla64 -> NASM via pipeline
            Console.WriteLine($"Compiling {sourceFile}...");
            var sourceText = File.ReadAllText(sourceFile);
            var nasmCode = CompilePipeline.EmitNasm(sourceFile, sourceText, options);
            File.WriteAllText(nasmFile, nasmCode);
            Console.WriteLine($"  -> {nasmFile}");

            // 2. Assemble NASM -> .o
            Console.WriteLine("Assembling with NASM...");
            if (!NasmTool.TryFindNasm(out var nasmPath))
            {
                Console.Error.WriteLine("Error: NASM not found. Install NASM (https://nasm.us)");
                Console.Error.WriteLine($"  NASM output saved at: {nasmFile}");
                return 1;
            }
            Console.WriteLine($"  NASM: {nasmPath}");

            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError))
            {
                Console.Error.WriteLine($"Assembly error:\n{nasmError}");
                return 1;
            }
            Console.WriteLine($"  -> {objFile}");

            // 3. Link .o -> executable
            Console.WriteLine("Linking...");
            if (!LinkerTool.TryLink(objFile, exeFile, out var linkError, out var requiresWslRun))
            {
                Console.Error.WriteLine($"Link error:\n{linkError}");
                return 1;
            }
            Console.WriteLine($"  -> {exeFile}");

            // 4. Make executable (if on Unix)
            if (!requiresWslRun)
            {
                try
                {
                    var chmod = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{exeFile}\"",
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(chmod)?.WaitForExit(2000);
                }
                catch
                {
                }
            }

            Console.WriteLine($"\nBuild successful: {exeFile}");
            return 0;
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