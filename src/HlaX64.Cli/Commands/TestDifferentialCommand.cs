using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class TestDifferentialCommand : Command<TestCommand.Settings>
{
    protected override int Execute(CommandContext context, TestCommand.Settings settings, CancellationToken cancellation)
    {
        if (settings.Directory == "tests/samples")
            settings.Directory = "tests/differential";
        return TestCommand.RunTests(settings);
    }
}
