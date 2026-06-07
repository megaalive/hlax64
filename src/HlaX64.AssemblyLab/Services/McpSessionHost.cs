using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Spawns HlaX64.McpServer and speaks MCP JSON-RPC over stdio.</summary>
public sealed class McpSessionHost : IDisposable
{
    private Process? _process;
    private Stream? _stdin;
    private Stream? _stdout;
    private CancellationTokenSource? _readerCts;
    private Task? _readerTask;
    private int _seq = 1;
    private readonly StringBuilder _log = new();
    private readonly object _logLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly ConcurrentDictionary<int, TaskCompletionSource<string>> _pending = new();

    public event Action<string>? MessageReceived;

    public bool IsRunning => _process is { HasExited: false };
    public string LogText
    {
        get
        {
            lock (_logLock)
                return _log.ToString();
        }
    }

    public Task StartAsync(string? repoRoot = null, CancellationToken cancellationToken = default)
    {
        return Task.Run(() => StartCore(repoRoot), cancellationToken);
    }

    public void Start(string? repoRoot = null) => StartCore(repoRoot);

    private void StartCore(string? repoRoot)
    {
        Stop();
        if (!TryStartFromDll(repoRoot, out _) &&
            !TryStartBundled(out _) &&
            !TryStartDotnet(repoRoot, out _))
        {
            Append("MCP: HlaX64.McpServer not found (build solution or publish Assembly Lab with MCP).");
            return;
        }

        Append("MCP: spawned HlaX64.McpServer (stdio)");
    }

    public Task<string> InitializeAsync(CancellationToken cancellationToken = default)
        => SendRequestAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "assemblylab", version = "1.0" }
        }, cancellationToken);

    public Task SendInitializedNotificationAsync()
        => SendNotificationAsync("notifications/initialized", new { });

    public Task<string> ListToolsAsync(CancellationToken cancellationToken = default)
        => SendRequestAsync("tools/list", new { }, cancellationToken);

    public Task<string> CallToolAsync(string name, object arguments, CancellationToken cancellationToken = default)
        => SendRequestAsync("tools/call", new { name, arguments }, cancellationToken);

    public async Task<string> ExplainCurrentSourceAsync(
        string sourcePath,
        string sourceText,
        string target,
        CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            Start(null);

        var tempFile = Path.Combine(Path.GetTempPath(), "hlax64-lab-mcp-" + Guid.NewGuid().ToString("N")[..8] + ".hla64");
        await File.WriteAllTextAsync(tempFile, sourceText, cancellationToken).ConfigureAwait(false);
        try
        {
            return await CallToolAsync("explain", new { source = tempFile, target }, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(tempFile); } catch { /* ignore */ }
        }
    }

    public void Stop()
    {
        foreach (var (_, tcs) in _pending)
            tcs.TrySetCanceled();
        _pending.Clear();

        _readerCts?.Cancel();
        try { _readerTask?.Wait(TimeSpan.FromSeconds(2)); } catch { /* ignore */ }

        try
        {
            if (_process is { HasExited: false })
                _process.Kill(entireProcessTree: true);
        }
        catch { /* ignore */ }

        _process?.Dispose();
        _process = null;
        _stdin = null;
        _stdout = null;
        _readerTask = null;
        _readerCts = null;
        _seq = 1;
    }

    private bool TryStartFromDll(string? repoRoot, out bool started)
    {
        started = false;
        repoRoot ??= FindRepoRoot();
        foreach (var config in new[] { "Debug", "Release" })
        {
            var dll = Path.Combine(repoRoot, "src", "HlaX64.McpServer", "bin", config, "net10.0", "HlaX64.McpServer.dll");
            if (!File.Exists(dll))
                continue;
            return StartProcess("dotnet", $"exec \"{dll}\"", out started, repoRoot);
        }
        return false;
    }

    private bool TryStartBundled(out bool started)
    {
        started = false;
        var baseDir = AppContext.BaseDirectory;
        foreach (var name in new[] { "HlaX64.McpServer.exe", "HlaX64.McpServer" })
        {
            var exe = Path.Combine(baseDir, name);
            if (!File.Exists(exe))
                continue;
            return StartProcess(exe, "", out started);
        }

        var bundledDll = Path.Combine(baseDir, "HlaX64.McpServer.dll");
        if (File.Exists(bundledDll))
            return StartProcess("dotnet", $"exec \"{bundledDll}\"", out started, baseDir);

        return false;
    }

    private bool TryStartDotnet(string? repoRoot, out bool started)
    {
        started = false;
        repoRoot ??= FindRepoRoot();
        var project = Path.Combine(repoRoot, "src", "HlaX64.McpServer", "HlaX64.McpServer.csproj");
        if (!File.Exists(project))
            return false;
        return StartProcess("dotnet", $"run --project \"{project}\" --no-build", out started, repoRoot)
               || StartProcess("dotnet", $"run --project \"{project}\"", out started, repoRoot);
    }

    private bool StartProcess(string fileName, string arguments, out bool started, string? workingDirectory = null)
    {
        started = false;
        try
        {
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
                    CreateNoWindow = true,
                    WorkingDirectory = workingDirectory ?? Directory.GetCurrentDirectory()
                },
                EnableRaisingEvents = true
            };
            _process.Start();
            _stdin = _process.StandardInput.BaseStream;
            _stdout = _process.StandardOutput.BaseStream;
            _readerCts = new CancellationTokenSource();
            _readerTask = Task.Run(() => ReadLoopAsync(_readerCts.Token));
            _ = ReadStreamAsync(_process.StandardError, _readerCts.Token, prefix: "stderr: ");
            started = true;
            return true;
        }
        catch (Exception ex)
        {
            Append($"MCP start failed: {ex.Message}");
            return false;
        }
    }

    private async Task<string> SendRequestAsync(string method, object parameters, CancellationToken cancellationToken)
    {
        if (_stdin == null || _stdout == null || !IsRunning)
            return "{\"error\":\"MCP not running\"}";

        var id = Interlocked.Increment(ref _seq);
        var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[id] = tcs;

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = id,
                ["method"] = method,
                ["params"] = parameters
            };
            WriteMessage(payload);
        }
        finally
        {
            _writeLock.Release();
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));
        await using var reg = timeoutCts.Token.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending))
                pending.TrySetResult("{\"error\":\"timeout\"}");
        });

        return await tcs.Task.ConfigureAwait(false);
    }

    private Task SendNotificationAsync(string method, object parameters)
    {
        if (_stdin == null)
            return Task.CompletedTask;

        var payload = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["method"] = method,
            ["params"] = parameters
        };
        WriteMessage(payload);
        return Task.CompletedTask;
    }

    private void WriteMessage(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        Append($"→ {json}");
        var bytes = Encoding.UTF8.GetBytes(json + "\n");
        _stdin!.Write(bytes, 0, bytes.Length);
        _stdin.Flush();
    }

    private async Task ReadLoopAsync(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested && _stdout != null)
            {
                var msg = await ReadOneMessageAsync(_stdout, token).ConfigureAwait(false);
                if (msg == null)
                    break;

                Append($"← {msg}");
                DispatchMessage(msg);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Append($"MCP read error: {ex.Message}");
        }
    }

    private void DispatchMessage(string msg)
    {
        try
        {
            using var doc = JsonDocument.Parse(msg);
            if (!doc.RootElement.TryGetProperty("id", out var idEl) ||
                idEl.ValueKind != JsonValueKind.Number)
                return;

            var id = idEl.GetInt32();
            if (_pending.TryRemove(id, out var tcs))
                tcs.TrySetResult(msg);
        }
        catch { /* ignore malformed */ }
    }

    private static async Task<string?> ReadOneMessageAsync(Stream stream, CancellationToken token)
    {
        var headerLines = new List<string>();
        while (true)
        {
            var line = await ReadLineAsync(stream, token).ConfigureAwait(false);
            if (line == null)
                return headerLines.Count > 0 ? string.Join('\n', headerLines) : null;
            if (line.TrimStart().StartsWith('{'))
                return line;
            if (line.Length == 0)
                break;
            headerLines.Add(line);
        }

        var header = string.Join('\n', headerLines);
        var match = System.Text.RegularExpressions.Regex.Match(
            header, @"Content-Length:\s*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var length = int.Parse(match.Groups[1].Value);
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), token)
                .ConfigureAwait(false);
            if (read <= 0)
                return null;
            offset += read;
        }

        return Encoding.UTF8.GetString(buffer);
    }

    private static async Task<string?> ReadLineAsync(Stream stream, CancellationToken token)
    {
        var sb = new StringBuilder();
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), token).ConfigureAwait(false);
            if (read <= 0)
                return sb.Length > 0 ? sb.ToString() : null;

            var b = one[0];
            if (b == '\r')
                continue;
            if (b == '\n')
                return sb.ToString();
            sb.Append((char)b);
        }
    }

    private async Task ReadStreamAsync(StreamReader reader, CancellationToken token, string prefix)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(token).ConfigureAwait(false);
                if (line == null) break;
                Append(prefix + line);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void Append(string line)
    {
        lock (_logLock)
            _log.AppendLine(line);
        MessageReceived?.Invoke(line);
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
