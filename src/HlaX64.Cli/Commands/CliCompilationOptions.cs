using HlaX64.Compiler.Options;

namespace HlaX64.Cli.Commands;

internal static class CliCompilationOptions
{
    internal static CompilationOptions FromCli(
        string? target,
        string? runtimeMode,
        bool warnBounds,
        bool warnDefinite = false,
        bool warnUnreachable = false,
        bool warnLiveness = false,
        bool warnVerify = false)
    {
        var options = CompilationOptions.Default with
        {
            Target = TargetTriple.Parse(target ?? "linux-x64-sysv")
        };

        if (runtimeMode?.Equals("library", StringComparison.OrdinalIgnoreCase) == true)
            options = options with { RuntimeMode = HlaX64.Compiler.Options.RuntimeMode.Library };

        var warnings = options.Warnings;
        if (warnBounds)
            warnings = warnings with { Bounds = true };
        if (warnVerify)
            warnings = warnings with { DefiniteAssignment = true, Unreachable = true, Liveness = true };
        else
        {
            if (warnDefinite)
                warnings = warnings with { DefiniteAssignment = true };
            if (warnUnreachable)
                warnings = warnings with { Unreachable = true };
            if (warnLiveness)
                warnings = warnings with { Liveness = true };
        }

        return options with { Warnings = warnings };
    }
}
