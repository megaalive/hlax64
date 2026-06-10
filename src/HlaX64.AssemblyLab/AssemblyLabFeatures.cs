namespace HlaX64.AssemblyLab;

using HlaX64.DebugAdapter;

/// <summary>Compile-time switches for experimental Assembly Lab capabilities.</summary>
public static class AssemblyLabFeatures
{
    /// <summary>
    /// Interactive stepping is enabled when a native debug backend is available
    /// (Win32 on Windows, GDB on Linux). Override with HLAX64_DEBUG=1/0 to force on/off.
    /// </summary>
    public static bool DebugEnabled => ResolveDebugEnabled();

    public static bool IsDebugEnvForced =>
        string.Equals(Environment.GetEnvironmentVariable("HLAX64_DEBUG"), "1", StringComparison.Ordinal);

    public static bool IsDebugEnvDisabled =>
        string.Equals(Environment.GetEnvironmentVariable("HLAX64_DEBUG"), "0", StringComparison.Ordinal);

    /// <summary>Fast probe used for status text (no stepping smoke).</summary>
    public static DebugCapabilityReport DebugCapabilities =>
        _debugCapabilities.Value;

    private static readonly Lazy<DebugCapabilityReport> _debugCapabilities =
        new(DebugCapabilityProbe.ProbeFast);

    public static string DebugDisabledMessage
    {
        get
        {
            if (DebugEnabled && IsDebugEnvForced)
                return "Debug override enabled (HLAX64_DEBUG=1).";

            if (DebugEnabled)
                return $"Debug enabled ({DebugCapabilities.Summary}).";

            if (IsDebugEnvDisabled)
                return "Debug disabled (HLAX64_DEBUG=0).";

            return $"Debug is unavailable ({DebugCapabilities.Summary}). Use Run instead, or set HLAX64_DEBUG=1 to override.";
        }
    }

    /// <summary>MCP stdio host for tool/explain integration; set false if the host misbehaves.</summary>
    public static readonly bool McpEnabled = true;

    public const string McpDisabledMessage =
        "MCP is temporarily disabled.";

    private static bool ResolveDebugEnabled()
    {
        if (IsDebugEnvForced)
            return true;

        if (IsDebugEnvDisabled)
            return false;

        return DebugBackendFactory.IsDefaultBackendAvailable();
    }
}
