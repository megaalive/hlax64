using System.ComponentModel;
using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
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

        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outputDir, $"{sourceName}.o");
        var exeFile = Path.Combine(outputDir, sourceName);

        try
        {
            // 1. Compile .hla64 -> NASM
            Console.WriteLine($"Compiling {sourceFile}...");
            var sourceText = File.ReadAllText(sourceFile);
            var lexer = new Lexer(sourceText);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var program = parser.Parse();
            var emitter = new NasmEmitter();
            var nasmCode = emitter.Emit(program);
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

            // 4. Make executable (if on Unix, not needed for WSL)
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
                    // chmod may not exist on Windows, that's fine
                }
            }

            Console.WriteLine($"\nBuild successful: {exeFile}");
            return 0;
        }
        catch (ParseException ex)
        {
            Console.Error.WriteLine($"Parse error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}