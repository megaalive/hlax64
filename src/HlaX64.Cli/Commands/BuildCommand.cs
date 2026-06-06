using System.ComponentModel;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class BuildCommand : Command<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Output directory")]
        [CommandOption("-o|--output")]
        public string? OutputDir { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        Console.WriteLine($"hla64 build: {settings.Source}");
        Console.WriteLine("Build command is not yet implemented.");
        return 0;
    }
}