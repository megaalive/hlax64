using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;

namespace HlaX64.DebugAdapter;

/// <summary>Shared process IO helpers for gdb/lldb debug backends.</summary>
public abstract class DebugEngineSession : IDebugBackend
{
    private Process? _process;
    private StreamWriter? _stdin;
    private CancellationTokenSource? _readerCts;
    private int _token = 100;
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();
    private readonly StringBuilder _recentOutput = new();
    private readonly object _outputLock = new();

    public abstract string Name { get; }
    public abstract bool IsAvailable { get; }

    public event Action<string>? OutputReceived;
    public event Action<DebugStopInfo>? Stopped;

    public bool IsEngineAlive =>
        !_engineExited && _process is { HasExited: false } && _stdin != null;

    bool IDebugBackend.IsEngineAlive => IsEngineAlive;

    private volatile bool _engineExited;

    protected Process? Process => _process;
    protected StreamWriter? Stdin => _stdin;

    protected void StartEngine(string fileName, string arguments)
    {
        _engineExited = false;
        _process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        _process.EnableRaisingEvents = true;
        _process.Exited += (_, _) => _engineExited = true;
        _process.Start();
        _stdin = _process.StandardInput;
        _readerCts = new CancellationTokenSource();
        _ = ReadStreamAsync(_process.StandardOutput, _readerCts.Token);
        _ = ReadStreamAsync(_process.StandardError, _readerCts.Token);
    }

    protected bool TryWriteLine(string cmd)
    {
        if (!IsEngineAlive)
        {
            OutputReceived?.Invoke($"> (debugger exited) skipped: {cmd}");
            return false;
        }

        try
        {
            _stdin!.WriteLine(cmd);
            _stdin.Flush();
            OutputReceived?.Invoke($"> {cmd}");
            return true;
        }
        catch (IOException ex)
        {
            _engineExited = true;
            OutputReceived?.Invoke($"debugger I/O closed: {ex.Message}");
            return false;
        }
    }

    protected void WriteLine(string cmd)
    {
        TryWriteLine(cmd);
    }

    protected void EmitOutput(string line)
    {
        OutputReceived?.Invoke(line);
    }

    protected string SendCommand(string command, int timeoutMs = 3000)
    {
        if (_stdin == null)
            return "";

        var token = Interlocked.Increment(ref _token);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[token] = tcs;
        WriteLine($"{token}{command}");

        if (!tcs.Task.Wait(timeoutMs))
        {
            _pending.TryRemove(token, out _);
            return "";
        }

        return tcs.Task.GetAwaiter().GetResult();
    }

    protected abstract void HandleEngineLine(string line);

    protected void CompletePending(int token, string payload)
    {
        if (_pending.TryRemove(token, out var tcs))
            tcs.TrySetResult(payload);
    }

    protected void AppendRecent(string line)
    {
        lock (_outputLock)
        {
            _recentOutput.AppendLine(line);
            if (_recentOutput.Length > 64_000)
                _recentOutput.Remove(0, _recentOutput.Length - 32_000);
        }
    }

    protected string RecentOutputSnapshot()
    {
        lock (_outputLock)
            return _recentOutput.ToString();
    }

    protected void RaiseStopped(DebugStopInfo info) => Stopped?.Invoke(info);

    public abstract void Launch(string executable, string[]? args = null);
    public abstract void SetBreakpoint(string file, int line);
    public abstract void SetBreakpointBySymbol(string symbol);
    public abstract void SetBreakpointAtAddress(string symbol, int byteOffset);
    public abstract void Continue();
    public abstract void StepOver();
    public abstract void StepInto();
    public abstract void StepOut();
    public abstract void Kill();

    public virtual IReadOnlyList<object> GetStackFrames()
    {
        if (!IsEngineAlive)
            return [];

        var frames = QueryStackFrames();
        return frames.Select(f => (object)new
        {
            id = f.Id,
            name = f.Name,
            line = f.Line,
            column = f.Column,
            source = f.FilePath != null ? new { path = f.FilePath } : null
        }).ToList();
    }

    public virtual IReadOnlyList<DebugRegister> GetRegisters()
    {
        if (!IsEngineAlive)
            return [];
        return QueryRegisters();
    }

    protected abstract IReadOnlyList<DebugStackFrame> QueryStackFrames();
    protected abstract IReadOnlyList<DebugRegister> QueryRegisters();

    public virtual void Disconnect()
    {
        try { WriteLine("-gdb-exit"); } catch { /* ignore */ }
        try { _process?.Kill(entireProcessTree: true); } catch { /* ignore */ }
        _process?.WaitForExit(2000);
    }

    public void Dispose()
    {
        _readerCts?.Cancel();
        Disconnect();
        _process?.Dispose();
    }

    private async Task ReadStreamAsync(StreamReader reader, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line == null) break;
                AppendRecent(line);
                OutputReceived?.Invoke(line);
                HandleEngineLine(line);
            }
        }
        catch (OperationCanceledException) { }
    }

    protected bool TryCompleteResultToken(string line)
    {
        if (line.Length < 3 || line[0] != '^')
            return false;

        var comma = line.IndexOf(',');
        if (comma <= 1)
            return false;

        if (!int.TryParse(line[1..comma], out var token))
            return false;

        CompletePending(token, line);
        return true;
    }
}
