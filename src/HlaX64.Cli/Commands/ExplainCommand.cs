using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Services;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class ExplainCommand : Command<ExplainCommand.Settings>
{
    public sealed class Settings : CommandSettings, IVerificationCliOptions
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
            ReportError(settings, $"Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceText = File.ReadAllText(settings.Source);
        var options = CliCompilationOptions.FromCli(
            settings.Target, null, settings.WarnBounds,
            settings.WarnDefinite, settings.WarnUnreachable, settings.WarnLiveness, settings.WarnVerify);
        var report = ExplainReport.Create(settings.Source, sourceText, options);

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = report.Success,
                version = Compilation.GetVersion(),
                source = report.SourcePath,
                target = settings.Target,
                diagnostics = report.StructuredDiagnostics.Select(d => new
                {
                    code = d.Code,
                    severity = d.Severity.ToString().ToLowerInvariant(),
                    message = d.Message,
                    line = d.Line,
                    column = d.Column,
                    suggestion = d.Suggestion
                }),
                ir = report.Success ? report.IrFunctions.Select(f => f.ToString()) : null,
                lowered = report.Success ? report.LoweredFunctions.Select(ExplainReport.DescribeLowered) : null,
                nasm = report.Nasm
            });
            return report.Success ? 0 : 1;
        }

        Console.WriteLine($"HlaX64 Explain — v{Compilation.GetVersion()}");
        Console.WriteLine($"Source: {report.SourcePath}");
        Console.WriteLine($"Target: {settings.Target}");
        Console.WriteLine(new string('=', 60));

        if (!report.Success)
        {
            Console.WriteLine("\n--- Diagnostics ---");
            foreach (var diag in report.Diagnostics)
                Console.WriteLine(diag);
            return 1;
        }

        Console.WriteLine("\n--- IR ---");
        foreach (var ir in report.IrFunctions)
            Console.WriteLine(ir);

        Console.WriteLine("\n--- ABI / Lowered ---");
        foreach (var lowered in report.LoweredFunctions)
            Console.WriteLine(ExplainReport.DescribeLowered(lowered));

        Console.WriteLine("\n--- NASM ---");
        Console.WriteLine(report.Nasm);

        return 0;
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
