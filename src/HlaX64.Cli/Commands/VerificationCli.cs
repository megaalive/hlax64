namespace HlaX64.Cli.Commands;

/// <summary>Shared -Wverify / -Wdefinite / -Wunreachable / -Wliveness CLI options.</summary>
internal interface IVerificationCliOptions
{
    bool WarnDefinite { get; set; }
    bool WarnUnreachable { get; set; }
    bool WarnLiveness { get; set; }
    bool WarnVerify { get; set; }
}

internal static class VerificationCli
{
    internal static void ApplyFlags(
        bool warnBounds,
        IVerificationCliOptions flags,
        out bool bounds,
        out bool definite,
        out bool unreachable,
        out bool liveness,
        out bool verify)
    {
        bounds = warnBounds;
        definite = flags.WarnDefinite;
        unreachable = flags.WarnUnreachable;
        liveness = flags.WarnLiveness;
        verify = flags.WarnVerify;
    }
}
