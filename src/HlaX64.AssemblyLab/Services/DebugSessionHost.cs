using System.Diagnostics;
using System.Text;
using HlaX64.DebugAdapter;

namespace HlaX64.AssemblyLab.Services;

public sealed class DebugSessionHost : IDisposable
{
    private Process? _cliProcess;
    private CancellationTokenSource? _readerCts;
    public event Action<string>? MessageReceived;

    public bool IsRunning => _cliProcess is { HasExited: false };

    public void StartInProcess(Action<string> onDapMessage)
    {
        onDapMessage("DAP: in-process host ready (Linux gdb backend when launching executable).");
        onDapMessage($"Capabilities: {System.Text.Json.JsonSerializer.Serialize(DebugAdapterHost.Capabilities)}");
    }

    public void StartCliProcess(string repoRoot)
    {
        Stop();
        var cliProject = Path.Combine(repoRoot, "src", "HlaX64.Cli", "HlaX64.Cli.csproj");
        if (!File.Exists(cliProject))
        {
            MessageReceived?.Invoke("Debug: CLI project not found; use in-process mode.");
            StartInProcess(MessageReceived ?? (_ => { }));
            return;
        }

        _cliProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{cliProject}\" -- debug --stdio",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = repoRoot
            },
            EnableRaisingEvents = true
        };
        _cliProcess.Start();
        _readerCts = new CancellationTokenSource();
        _ = ReadStreamAsync(_cliProcess.StandardOutput, _readerCts.Token);
        _ = ReadStreamAsync(_cliProcess.StandardError, _readerCts.Token);
        MessageReceived?.Invoke("DAP: spawned hla64 debug --stdio");
    }

    public void SendInitialize()
    {
        var init = """{"seq":1,"type":"request","command":"initialize","arguments":{"clientID":"assemblylab","adapterID":"hla64"}}""";
        MessageReceived?.Invoke($"→ {init}");
        if (_cliProcess?.StandardInput.BaseStream.CanWrite == true)
        {
            _cliProcess.StandardInput.WriteLine(init);
            _cliProcess.StandardInput.Flush();
        }
    }

    public void Stop()
    {
        _readerCts?.Cancel();
        try
        {
            if (_cliProcess is { HasExited: false })
                _cliProcess.Kill(entireProcessTree: true);
        }
        catch { /* ignore */ }
        _cliProcess?.Dispose();
        _cliProcess = null;
    }

    private async Task ReadStreamAsync(StreamReader reader, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line == null) break;
                MessageReceived?.Invoke($"← {line}");
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageReceived?.Invoke($"DAP read error: {ex.Message}");
        }
    }

    public void Dispose() => Stop();
}
