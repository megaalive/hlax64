using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Services;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class DiffCommand : Command<DiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<old>")]
        public string OldSource { get; set; } = string.Empty;

        [CommandArgument(1, "<new>")]
        public string NewSource { get; set; } = string.Empty;

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.OldSource) || !File.Exists(settings.NewSource))
        {
            Report(settings, false, error: "Both source files must exist.");
            return 1;
        }

        var oldText = File.ReadAllText(settings.OldSource);
        var newText = File.ReadAllText(settings.NewSource);
        var diff = SemanticDiffService.Compare(oldText, newText);

        Report(settings, true, diff);
        return 0;
    }

    private static void Report(Settings settings, bool success, object? diff = null, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                diff,
                error
            });
            return;
        }

        if (!success)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        Console.WriteLine("Semantic diff complete (use --json for details).");
    }
}
