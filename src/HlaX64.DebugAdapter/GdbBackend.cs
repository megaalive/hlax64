using System.Diagnostics;

namespace HlaX64.DebugAdapter;

public sealed class GdbBackend : IDebugBackend
{
    private Process? _gdb;
    private StreamWriter? _stdin;
    private readonly List<string> _breakpoints = [];

    public string Name => "gdb";
    public bool IsAvailable => OperatingSystem.IsLinux() && File.Exists("/usr/bin/gdb");

    public void Launch(string executable, string[]? args = null)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("gdb backend requires Linux with gdb installed.");

        _gdb = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gdb",
                Arguments = "--interpreter=mi2 --quiet",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        _gdb.Start();
        _stdin = _gdb.StandardInput;
        Write($"file \"{executable}\"");
        foreach (var bp in _breakpoints)
            Write(bp);
        Write("start");
    }

    public void SetBreakpoint(string file, int line)
    {
        var cmd = $"break \"{file}\":{line}";
        _breakpoints.Add(cmd);
        if (_stdin != null)
            Write(cmd);
    }

    public IReadOnlyList<object> GetStackFrames()
    {
        if (_stdin == null)
            return [new { id = 1, name = "_start", line = 1, column = 1 }];
        Write("-stack-list-frames");
        return
        [
            new { id = 1, name = "_start", line = 1, column = 1, source = new { path = "main.hla64" } }
        ];
    }

    public void Continue() => Write("exec-continue");

    public void Disconnect()
    {
        try { Write("quit"); } catch { /* ignore */ }
        _gdb?.Kill(entireProcessTree: true);
    }

    private void Write(string cmd)
    {
        _stdin?.WriteLine(cmd);
        _stdin?.Flush();
    }

    public void Dispose()
    {
        Disconnect();
        _gdb?.Dispose();
    }
}
