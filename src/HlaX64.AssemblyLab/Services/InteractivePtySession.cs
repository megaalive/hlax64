using Porta.Pty;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Cross-platform interactive PTY session (ConPTY on Windows, POSIX PTY on Unix).</summary>
public sealed class InteractivePtySession : IDisposable
{
    private IPtyConnection? _connection;
    private CancellationTokenSource? _readCts;
    private Task? _readTask;
    private bool _disposed;

    public event Action<byte[]>? DataReceived;
    public event Action<int>? Exited;

    public bool IsRunning => _connection != null && !_disposed;

    public async Task StartAsync(string workingDirectory, int cols, int rows, string? repoRoot, CancellationToken cancellationToken = default)
    {
        Stop();

        var shell = PlatformShellProfile.Resolve();
        var options = new PtyOptions
        {
            Name = "hlax64-lab",
            Cols = Math.Max(cols, 1),
            Rows = Math.Max(rows, 1),
            Cwd = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory,
            App = shell.Executable,
            CommandLine = shell.Arguments,
            Environment = PlatformShellProfile.BuildEnvironment(workingDirectory, repoRoot)
        };

        _connection = await PtyProvider.SpawnAsync(options, cancellationToken).ConfigureAwait(false);
        _connection.ProcessExited += (_, e) => Exited?.Invoke(e.ExitCode);

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _readTask = Task.Run(() => PumpOutputAsync(_readCts.Token), _readCts.Token);
    }

    public void Send(ReadOnlySpan<byte> data)
    {
        if (_connection == null || data.IsEmpty)
            return;

        try
        {
            _connection.WriterStream.Write(data);
            _connection.WriterStream.Flush();
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    public void SendLine(string line)
    {
        var newline = OperatingSystem.IsWindows() ? "\r" : "\n";
        Send(System.Text.Encoding.UTF8.GetBytes(line + newline));
    }

    public void Resize(int cols, int rows)
    {
        try
        {
            _connection?.Resize(Math.Max(cols, 1), Math.Max(rows, 1));
        }
        catch (ObjectDisposedException) { }
    }

    public void Stop()
    {
        _readCts?.Cancel();

        try
        {
            _readTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch { /* ignore */ }

        _readCts?.Dispose();
        _readCts = null;
        _readTask = null;

        if (_connection != null)
        {
            try
            {
                _connection.Kill();
            }
            catch { /* ignore */ }

            _connection.Dispose();
            _connection = null;
        }
    }

    private async Task PumpOutputAsync(CancellationToken token)
    {
        if (_connection == null)
            return;

        var buffer = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested)
            {
                var read = await _connection.ReaderStream.ReadAsync(buffer.AsMemory(0, buffer.Length), token)
                    .ConfigureAwait(false);
                if (read <= 0)
                    break;

                var chunk = buffer.AsSpan(0, read).ToArray();
                DataReceived?.Invoke(chunk);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
    }
}
