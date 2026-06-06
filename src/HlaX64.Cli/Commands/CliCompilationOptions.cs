using HlaX64.Compiler.Options;

namespace HlaX64.Cli.Commands;

internal static class CliCompilationOptions
{
    internal static CompilationOptions FromCli(
        string? target,
        string? runtimeMode,
        bool warnBounds)
    {
        var options = CompilationOptions.Default with
        {
            Target = TargetTriple.Parse(target ?? "linux-x64-sysv")
        };

        if (runtimeMode?.Equals("library", StringComparison.OrdinalIgnoreCase) == true)
            options = options with { RuntimeMode = HlaX64.Compiler.Options.RuntimeMode.Library };

        if (warnBounds)
            options = options with { Warnings = options.Warnings with { Bounds = true } };

        return options;
    }
}
