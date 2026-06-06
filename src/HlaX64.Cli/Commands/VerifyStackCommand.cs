using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Verification;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class VerifyStackCommand : Command<VerifyStackCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Target triple")]
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
            return ReportFail(settings, $"Source file '{settings.Source}' not found.");

        var options = CliCompilationOptions.FromCli(settings.Target, null, warnBounds: false);
        var sourceText = File.ReadAllText(settings.Source);
        var compile = new Compilation(settings.Source, sourceText, options).Process();
        if (!compile.Success)
            return ReportFail(settings, string.Join('\n', compile.Diagnostics));

        var result = StackVerifier.Verify(compile.IrFunctions, compile.LoweredFunctions);

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = result.Success,
                version = Compilation.GetVersion(),
                source = Path.GetFullPath(settings.Source),
                target = settings.Target,
                procedures = result.Procedures.Select(p => new
                {
                    name = p.Procedure,
                    stackFrameBytes = p.StackFrameSizeBytes,
                    localSlots = p.LocalSlotCount,
                    hasPrologue = p.HasPrologue,
                    hasEpilogue = p.HasEpilogue,
                    alignmentOk = p.AlignmentOk
                }),
                issues = result.Issues.Select(i => new { code = i.Code, message = i.Message, procedure = i.Procedure })
            });
        }
        else
        {
            Console.WriteLine($"Stack verification — {settings.Source}");
            Console.WriteLine($"Target: {settings.Target}");
            foreach (var p in result.Procedures)
            {
                Console.WriteLine($"  {p.Procedure}: frame={p.StackFrameSizeBytes}B prologue={p.HasPrologue} epilogue={p.HasEpilogue} aligned={p.AlignmentOk}");
            }

            foreach (var issue in result.Issues)
                Console.WriteLine($"  [{issue.Code}] {issue.Procedure}: {issue.Message}");

            Console.WriteLine(result.Success ? "OK" : "FAILED");
        }

        return result.Success ? 0 : 1;
    }

    private static int ReportFail(Settings settings, string error)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = false,
                version = Compilation.GetVersion(),
                error
            });
        }
        else
        {
            Console.Error.WriteLine($"Error: {error}");
        }

        return 1;
    }
}
