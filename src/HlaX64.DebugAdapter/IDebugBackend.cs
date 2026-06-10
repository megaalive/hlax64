using HlaX64.Compiler.Debug;

namespace HlaX64.DebugAdapter;

public interface IDebugBackend : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    bool IsEngineAlive { get; }
    event Action<string>? OutputReceived;
    event Action<DebugStopInfo>? Stopped;
    void Launch(string executable, string[]? args = null);
    void SetBreakpoint(string file, int line);
    void SetBreakpointBySymbol(string symbol);
    void SetBreakpointAtAddress(string symbol, int byteOffset);
    void Continue();
    void StepOver();
    void StepInto();
    void StepOut();
    void Kill();
    IReadOnlyList<object> GetStackFrames();
    IReadOnlyList<DebugRegister> GetRegisters();
    void Disconnect();
}

public static class DebugBackendFactory
{
    public static IDebugBackend CreateDefault()
    {
        if (OperatingSystem.IsLinux())
            return new GdbBackend();

        if (OperatingSystem.IsWindows())
        {
            if (Win32DebugBackend.IsSupported)
                return new Win32DebugBackend();
            if (DebuggerProbe.TryFindGdb(out _))
                return new GdbBackend();
            if (DebuggerProbe.IsLldbUsable(out _))
                return new LldbBackend();
            return new GdbBackend();
        }

        return new GdbBackend();
    }

    public static void PrepareExecutable(
        IDebugBackend backend,
        string executable,
        string? nasmPath = null,
        SourceMapDocument? sourceMap = null)
    {
        switch (backend)
        {
            case GdbBackend gdb:
                gdb.PrepareExecutable(executable);
                break;
            case Win32DebugBackend win32:
                win32.PrepareExecutable(executable);
                if (!string.IsNullOrWhiteSpace(nasmPath))
                    win32.PrepareDebugMaps(executable, nasmPath, sourceMap);
                break;
        }
    }

    public static ulong? TryGetCurrentRip(IDebugBackend backend)
        => backend switch
        {
            GdbBackend gdb => gdb.TryGetCurrentRip(),
            Win32DebugBackend win32 => win32.TryGetCurrentRip(),
            _ => null
        };

    public static int? TryResolveUserCallSiteSourceLine(
        IDebugBackend backend,
        string executablePath,
        string nasmPath,
        SourceMapDocument? sourceMap)
        => backend is Win32DebugBackend win32
            ? win32.TryResolveUserCallSiteSourceLine(executablePath, nasmPath, sourceMap)
            : null;

    public static bool IsProgramShutdownPhase(
        IDebugBackend backend,
        ulong rip,
        string executablePath,
        string nasmPath,
        SourceMapDocument? sourceMap)
    {
        if (backend is not Win32DebugBackend win32)
            return false;

        var maps = PeDebugAddressMap.GetOrBuild(executablePath, nasmPath, sourceMap);
        var callSite = win32.TryResolveUserCallSiteSourceLine(executablePath, nasmPath, sourceMap);
        return PeDebugAddressMap.IsProgramShutdownPhase(rip, executablePath, nasmPath, maps, callSite);
    }

    public static bool IsDefaultBackendAvailable()
    {
        if (OperatingSystem.IsWindows() && Win32DebugBackend.IsSupported)
            return true;

        if (OperatingSystem.IsLinux())
            return DebuggerProbe.TryFindGdb(out _);

        return DebuggerProbe.TryFindGdb(out _) || DebuggerProbe.IsLldbUsable(out _);
    }

    public static string? GetUnavailableReason()
    {
        if (OperatingSystem.IsWindows() && Win32DebugBackend.IsSupported)
            return null;

        return DebuggerProbe.GetUnavailableReason();
    }
}
