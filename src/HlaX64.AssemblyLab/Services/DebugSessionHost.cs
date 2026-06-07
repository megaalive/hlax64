using System.Diagnostics;
using System.Text.Json;

namespace HlaX64.AssemblyLab.Services;

public sealed class DebugSessionHost : IDisposable
{
    private Process? _cliProcess;
    private CancellationTokenSource? _readerCts;
    private int _seq = 1;

    public event Action<string>? MessageReceived;

    public bool IsRunning => _cliProcess is { HasExited: false };

    public void StartInProcess(Action<string> onDapMessage)
    {
        onDapMessage("DAP: in-process host ready (gdb on Linux, lldb on Windows).");
        onDapMessage($"Capabilities: {JsonSerializer.Serialize(HlaX64.DebugAdapter.DebugAdapterHost.Capabilities)}");
    }

    public void StartCliProcess(string? repoRoot = null)
    {
        Stop();
        if (!TryStartBundledCli(out var started) && !TryStartDotnetCli(repoRoot, out started))
        {
            MessageReceived?.Invoke("Debug: CLI not found; use in-process mode.");
            StartInProcess(MessageReceived ?? (_ => { }));
            return;
        }

        if (started)
            MessageReceived?.Invoke("DAP: spawned hla64 debug --stdio");
    }

    public void SendRequest(string command, object? arguments = null)
    {
        var payload = new Dictionary<string, object?>
        {
            ["seq"] = _seq++,
            ["type"] = "request",
            ["command"] = command
        };
        if (arguments != null)
            payload["arguments"] = arguments;

        var json = JsonSerializer.Serialize(payload);
        MessageReceived?.Invoke($"→ {json}");
        if (_cliProcess?.StandardInput.BaseStream.CanWrite == true)
        {
            _cliProcess.StandardInput.WriteLine(json);
            _cliProcess.StandardInput.Flush();
        }
    }

    public void SendInitialize()
        => SendRequest("initialize", new { clientID = "assemblylab", adapterID = "hla64" });

    public void SendLaunch(string program)
        => SendRequest("launch", new { program });

    public void SendSetBreakpoints(string sourcePath, IEnumerable<int> lines)
    {
        var breakpoints = lines.Select(l => new { line = l }).ToArray();
        SendRequest("setBreakpoints", new
        {
            source = new { path = sourcePath },
            breakpoints
        });
    }

    public void SendConfigurationDone()
        => SendRequest("configurationDone");

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
        _seq = 1;
    }

    private bool TryStartBundledCli(out bool started)
    {
        started = false;
        var baseDir = AppContext.BaseDirectory;
        foreach (var name in new[] { "HlaX64.Cli.exe", "HlaX64.Cli" })
        {
            var cliExe = Path.Combine(baseDir, name);
            if (!File.Exists(cliExe))
                continue;
            return StartProcess(cliExe, "debug --stdio", out started);
        }
        return false;
    }

    private bool TryStartDotnetCli(string? repoRoot, out bool started)
    {
        started = false;
        repoRoot ??= FindRepoRoot();
        var cliProject = Path.Combine(repoRoot, "src", "HlaX64.Cli", "HlaX64.Cli.csproj");
        if (!File.Exists(cliProject))
            return false;
        return StartProcess("dotnet", $"run --project \"{cliProject}\" -- debug --stdio", out started, repoRoot);
    }

    private bool StartProcess(string fileName, string arguments, out bool started, string? workingDirectory = null)
    {
        started = false;
        _cliProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
            },
            EnableRaisingEvents = true
        };
        _cliProcess.Start();
        _readerCts = new CancellationTokenSource();
        _ = ReadStreamAsync(_cliProcess.StandardOutput, _readerCts.Token);
        _ = ReadStreamAsync(_cliProcess.StandardError, _readerCts.Token);
        started = true;
        return true;
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

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "HlaX64.slnx")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Directory.GetCurrentDirectory();
    }

    public void Dispose() => Stop();
}
