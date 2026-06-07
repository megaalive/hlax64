using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Services;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class PlanCommand : Command<PlanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [CommandOption("--target")]
        public string? Target { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            Report(settings, false, error: $"Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceFile = Path.GetFullPath(settings.Source);
        var target = settings.Target ?? "linux-x64-sysv";
        var plan = PlanService.BuildPlan(sourceFile, target);

        Report(settings, true, plan, sourceFile, target);
        return 0;
    }

    private static void Report(Settings settings, bool success, object? plan = null, string? sourceFile = null,
        string? target = null, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                plan,
                error
            });
            return;
        }

        if (!success)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        Console.WriteLine(PlanService.FormatPlanText(sourceFile!, target));
    }
}
