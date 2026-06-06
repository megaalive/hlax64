using System.ComponentModel;
using System.Text;
using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Options;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

/// <summary>
/// Shows how HlaX64 lowers source through IR, ABI assignment, and NASM.
/// </summary>
public sealed class ExplainCommand : Command<ExplainCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Target triple: linux-x64-sysv (default) or windows-x64-msabi")]
        [CommandOption("-t|--target")]
        [DefaultValue("linux-x64-sysv")]
        public string Target { get; set; } = "linux-x64-sysv";

        [Description("Output as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            ReportError(settings, $"Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceText = File.ReadAllText(settings.Source);
        var targetTriple = TargetTriple.Parse(settings.Target);
        var options = CompilationOptions.Default with { Target = targetTriple };

        var result = CompilePipeline.Process(settings.Source, sourceText, options);
        string? nasm = null;
        if (result.Success)
        {
            var emitter = new NasmEmitter();
            nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals);
        }

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = result.Success,
                version = Compilation.GetVersion(),
                source = Path.GetFullPath(settings.Source),
                target = settings.Target,
                diagnostics = result.StructuredDiagnostics.Select(d => new
                {
                    code = d.Code,
                    severity = d.Severity.ToString().ToLowerInvariant(),
                    message = d.Message,
                    line = d.Line,
                    column = d.Column,
                    suggestion = d.Suggestion
                }),
                ir = result.Success ? result.IrFunctions.Select(f => f.ToString()) : null,
                lowered = result.Success ? result.LoweredFunctions.Select(DescribeLowered) : null,
                nasm
            });
            return result.Success ? 0 : 1;
        }

        Console.WriteLine($"HlaX64 Explain — v{Compilation.GetVersion()}");
        Console.WriteLine($"Source: {Path.GetFullPath(settings.Source)}");
        Console.WriteLine($"Target: {settings.Target}");
        Console.WriteLine(new string('=', 60));

        if (!result.Success)
        {
            Console.WriteLine("\n--- Diagnostics ---");
            foreach (var diag in result.Diagnostics)
                Console.WriteLine(diag);
            return 1;
        }

        Console.WriteLine("\n--- IR ---");
        foreach (var ir in result.IrFunctions)
            Console.WriteLine(ir);

        Console.WriteLine("\n--- ABI / Lowered ---");
        foreach (var lowered in result.LoweredFunctions)
            Console.WriteLine(DescribeLowered(lowered));

        Console.WriteLine("\n--- NASM ---");
        Console.WriteLine(nasm);

        return 0;
    }

    private static string DescribeLowered(LoweredFunction fn)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"function {fn.Name}:");
        sb.AppendLine($"  entry: {fn.IsEntryPoint}, export: {fn.IsExport}");
        sb.AppendLine($"  stack frame: {fn.StackFrameSize} bytes");
        if (fn.Parameters.Count > 0)
            sb.AppendLine($"  parameters: {string.Join(", ", fn.Parameters.Select(p => p.Name))}");
        if (fn.PreservedRegisters.Count > 0)
            sb.AppendLine($"  preserved: {string.Join(", ", fn.PreservedRegisters)}");
        if (fn.RequiredExterns.Count > 0)
            sb.AppendLine($"  externs: {string.Join(", ", fn.RequiredExterns)}");
        foreach (var block in fn.Blocks)
        {
            sb.AppendLine($"  {block.Label}:");
            foreach (var inst in block.Instructions)
                sb.AppendLine($"    {inst.AsmText}");
        }
        return sb.ToString().TrimEnd();
    }

    private static void ReportError(Settings settings, string message)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = false,
                version = Compilation.GetVersion(),
                error = message
            });
        }
        else
        {
            Console.Error.WriteLine($"Error: {message}");
        }
    }
}
