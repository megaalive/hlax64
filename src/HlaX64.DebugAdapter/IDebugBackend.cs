namespace HlaX64.DebugAdapter;

public interface IDebugBackend : IDisposable
{
    string Name { get; }
    bool IsAvailable { get; }
    void Launch(string executable, string[]? args = null);
    void SetBreakpoint(string file, int line);
    void Continue();
    IReadOnlyList<object> GetStackFrames();
    void Disconnect();
}

public static class DebugBackendFactory
{
    public static IDebugBackend CreateDefault()
    {
        if (OperatingSystem.IsLinux())
            return new GdbBackend();
        if (OperatingSystem.IsWindows())
            return new LldbBackend();
        return new GdbBackend();
    }
}
