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
            if (DebuggerProbe.TryFindGdb(out _))
                return new GdbBackend();
            if (DebuggerProbe.IsLldbUsable(out _))
                return new LldbBackend();
            return new GdbBackend();
        }

        return new GdbBackend();
    }

    public static string? GetUnavailableReason() => DebuggerProbe.GetUnavailableReason();
}
