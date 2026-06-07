using System.Diagnostics;

namespace HlaX64.DebugAdapter;

public sealed class LldbBackend : IDebugBackend
{
    private Process? _lldb;
    private StreamWriter? _stdin;
    private readonly List<string> _breakpoints = [];

    public string Name => "lldb";
    public bool IsAvailable => TryFindLldb(out _);

    public static bool TryFindLldb(out string path)
    {
        path = "";
        if (!OperatingSystem.IsWindows())
            return false;

        var llvmPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "LLVM", "bin", "lldb.exe");
        if (File.Exists(llvmPath))
        {
            path = llvmPath;
            return true;
        }

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), "lldb.exe");
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        return false;
    }

    public void Launch(string executable, string[]? args = null)
    {
        if (!TryFindLldb(out var lldbPath))
            throw new InvalidOperationException(
                "lldb backend requires Windows with lldb on PATH or in Program Files/LLVM/bin.");

        _lldb = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = lldbPath,
                Arguments = "-b -Q",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        _lldb.Start();
        _stdin = _lldb.StandardInput;
        Write($"target create \"{executable}\"");
        foreach (var bp in _breakpoints)
            Write(bp);
        Write("run");
    }

    public void SetBreakpoint(string file, int line)
    {
        var cmd = $"breakpoint set -f \"{file}\" -l {line}";
        _breakpoints.Add(cmd);
        if (_stdin != null)
            Write(cmd);
    }

    public IReadOnlyList<object> GetStackFrames()
    {
        if (_stdin == null)
            return [new { id = 1, name = "main", line = 1, column = 1 }];
        Write("thread backtrace");
        return
        [
            new { id = 1, name = "main", line = 1, column = 1, source = new { path = "main.hla64" } }
        ];
    }

    public void Continue() => Write("process continue");

    public void Disconnect()
    {
        try { Write("quit"); } catch { /* ignore */ }
        _lldb?.Kill(entireProcessTree: true);
    }

    private void Write(string cmd)
    {
        _stdin?.WriteLine(cmd);
        _stdin?.Flush();
    }

    public void Dispose()
    {
        Disconnect();
        _lldb?.Dispose();
    }
}
