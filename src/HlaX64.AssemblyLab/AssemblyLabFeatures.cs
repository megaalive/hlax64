namespace HlaX64.AssemblyLab;

/// <summary>Compile-time switches for experimental Assembly Lab capabilities.</summary>
public static class AssemblyLabFeatures
{
    /// <summary>
    /// Interactive stepping via GDB/LLDB on Windows is unreliable (MinGW attach + MI stepping exits immediately).
    /// Re-enable when a native Win32 debug backend lands (see rfcs/0024-assembly-lab.md).
    /// </summary>
    public static readonly bool DebugEnabled = false;

    public const string DebugDisabledMessage =
        "Debug is temporarily disabled (GDB stepping on Windows PE is unreliable). Use Run instead.";

    /// <summary>MCP stdio host for tool/explain integration; set false if the host misbehaves.</summary>
    public static readonly bool McpEnabled = true;

    public const string McpDisabledMessage =
        "MCP is temporarily disabled.";
}
