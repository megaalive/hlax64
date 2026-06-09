using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Services;
using HlaX64.Compiler;
using Spectre.Console;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class DoctorCommand : Command<DoctorCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Output results as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var report = DoctorReport.Run();

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = report.Success,
                version = report.Version,
                checks = report.Checks
            });
            return report.Success ? 0 : 1;
        }

        AnsiConsole.MarkupLine($"[bold]HlaX64 Doctor[/] — v{report.Version}");
        AnsiConsole.MarkupLine("");

        foreach (var r in report.Checks)
        {
            var icon = r.Passed ? "[green]\u2714[/]" : "[red]\u2718[/]";
            var source = string.IsNullOrWhiteSpace(r.Source) ? "" : $" [grey]({Markup.Escape(r.Source)})[/]";
            AnsiConsole.MarkupLine($"  {icon} {Markup.Escape(r.Name)}{source}: {Markup.Escape(r.Message)}");
            if (!string.IsNullOrWhiteSpace(r.InstallHint))
                AnsiConsole.MarkupLine($"     [grey]fix:[/] {Markup.Escape(r.InstallHint)}");
        }

        AnsiConsole.MarkupLine(report.Success
            ? "\n[green]All checks passed.[/]"
            : "\n[yellow]Some checks failed. See details above.[/]");

        return report.Success ? 0 : 1;
    }
}
