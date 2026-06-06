using System.ComponentModel;
using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public static class CompilePipeline
{
    public static string EmitNasm(string sourcePath, string sourceText, CompilationOptions? options = null)
    {
        var compilation = new Compilation(sourcePath, sourceText, options);
        var result = compilation.Process();

        if (!result.Success)
            throw new InvalidOperationException(
                string.Join("\n", result.Diagnostics));

        var emitter = new NasmEmitter();
        return emitter.Emit(result.LoweredFunctions, result.StringLiterals);
    }

    public static CompilationResult Process(string sourcePath, string sourceText, CompilationOptions? options = null)
    {
        var compilation = new Compilation(sourcePath, sourceText, options);
        return compilation.Process();
    }
}

public sealed class EmitNasmCommand : Command<EmitNasmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Output NASM file path (default: stdout)")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; set; }

        [Description("Runtime mode: inline (default) or library")]
        [CommandOption("--runtime")]
        public string? RuntimeMode { get; set; }

        [Description("Target triple: linux-x64-sysv (default) or windows-x64-msabi")]
        [CommandOption("--target")]
        public string? Target { get; set; }

        [Description("Warn when a literal array index may be out of bounds")]
        [CommandOption("-Wbounds|--warn-bounds")]
        public bool WarnBounds { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            Console.Error.WriteLine($"Error: Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceText = File.ReadAllText(settings.Source);
        var options = CliCompilationOptions.FromCli(settings.Target, settings.RuntimeMode, settings.WarnBounds);

        try
        {
            var nasmOutput = CompilePipeline.EmitNasm(settings.Source, sourceText, options);

            if (settings.OutputPath != null)
            {
                File.WriteAllText(settings.OutputPath, nasmOutput);
                Console.WriteLine($"NASM output written to: {settings.OutputPath}");
            }
            else
            {
                Console.WriteLine(nasmOutput);
            }

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