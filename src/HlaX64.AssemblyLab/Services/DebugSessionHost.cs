using System.Diagnostics;
using System.Text.Json;
using HlaX64.Compiler.Debug;
using HlaX64.DebugAdapter;

namespace HlaX64.AssemblyLab.Services;

public sealed class DebugSessionHost : IDisposable
{
    private Process? _cliProcess;
    private CancellationTokenSource? _readerCts;
    private IDebugBackend? _directBackend;
    private int _seq = 1;

    public event Action<string>? MessageReceived;
    public event Action<DebugStopInfo>? DebugStopped;

    public bool IsRunning => _directBackend != null || _cliProcess is { HasExited: false };

    public void StartInProcess(Action<string> onDapMessage)
    {
        onDapMessage("DAP: in-process host ready (win32 on Windows, gdb on Linux).");
        onDapMessage($"Capabilities: {JsonSerializer.Serialize(DebugAdapterHost.Capabilities)}");
    }

    public DebugStopInfo? LastStopInfo { get; private set; }

    public bool StartDirectBackend()
    {
        Stop();
        _directBackend = DebugBackendFactory.CreateDefault();
        if (!_directBackend.IsAvailable)
        {
            var reason = DebugBackendFactory.GetUnavailableReason()
                         ?? "Install MinGW GDB (Windows) or gdb (Linux).";
            MessageReceived?.Invoke($"Debug: no native debugger. {reason}");
            _directBackend.Dispose();
            _directBackend = null;
            return false;
        }

        _directBackend.OutputReceived += line =>
        {
            if (DebugOutputFilter.ShouldShow(line))
                MessageReceived?.Invoke(line);
        };
        _directBackend.Stopped += info =>
        {
            LastStopInfo = info;
            DebugStopped?.Invoke(info);
            MessageReceived?.Invoke(FormatStoppedSummary(info));
        };
        MessageReceived?.Invoke($"DAP: direct {_directBackend.Name} backend ready.");
        return true;
    }

    public void PrepareDirect(string program)
        => DebugBackendFactory.PrepareExecutable(_directBackend!, program);

    private static string FormatStoppedSummary(DebugStopInfo info)
    {
        var frame = info.Frames.FirstOrDefault();
        var addr = frame?.Address ?? "?";
        var func = frame?.Name ?? "?";
        var reason = info.Reason;

        if (reason is "signal-received" or "exited-signalled"
            && (func.Contains("KernelBase", StringComparison.OrdinalIgnoreCase)
                || func.Contains("ntdll", StringComparison.OrdinalIgnoreCase)
                || func.Contains("TestCreate", StringComparison.OrdinalIgnoreCase)))
        {
            return "← STOPPED  program already finished (crash during Windows shutdown). " +
                   "Restart Debug and use Step Over from entry — do not Continue past ExitProcess.";
        }

        if (reason == "breakpoint-hit")
        {
            return $"← STOPPED at entry/instruction  rip={addr}  ({func})  — use Step Over to advance";
        }

        return $"← STOPPED  reason={reason}  rip={addr}  in={func}";
    }

    public void ApplyResolvedBreakpoints(IEnumerable<ResolvedDebugBreakpoint> breakpoints)
    {
        if (_directBackend == null)
            return;

        var seenSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var bp in breakpoints)
        {
            switch (bp.Kind)
            {
                case "nasm-line" when !string.IsNullOrWhiteSpace(bp.FilePath):
                    _directBackend.SetBreakpoint(bp.FilePath!, bp.Line);
                    break;
                case "symbol" when !string.IsNullOrWhiteSpace(bp.Symbol):
                    if (!seenSymbols.Add(bp.Symbol!))
                        break;
                    _directBackend.SetBreakpointBySymbol(bp.Symbol!);
                    break;
                default:
                    if (seenSymbols.Add("_start"))
                        _directBackend.SetBreakpointBySymbol("_start");
                    break;
            }
        }
    }

    public ulong? GetCurrentInstructionPointer()
        => _directBackend == null ? null : DebugBackendFactory.TryGetCurrentRip(_directBackend);

    public int? TryResolveUserCallSiteSourceLine(
        string executablePath,
        string nasmPath,
        SourceMapDocument? sourceMap)
        => _directBackend == null
            ? null
            : DebugBackendFactory.TryResolveUserCallSiteSourceLine(
                _directBackend,
                executablePath,
                nasmPath,
                sourceMap);

    public bool IsProgramShutdownPhase(
        ulong rip,
        string executablePath,
        string nasmPath,
        SourceMapDocument? sourceMap)
        => _directBackend != null
           && DebugBackendFactory.IsProgramShutdownPhase(
               _directBackend,
               rip,
               executablePath,
               nasmPath,
               sourceMap);

    public void LogUserAction(string action, string? detail = null)
    {
        MessageReceived?.Invoke(detail == null
            ? $"→ USER {action}"
            : $"→ USER {action}  {detail}");
    }

    public void LaunchDirect(string program, string[]? args = null)
    {
        var argsText = args is { Length: > 0 }
            ? $"args: {Win32ProcessCommandLine.FormatArgsForLog(args)}"
            : "args: (none)";
        LogUserAction("Debug Start", argsText);
        _directBackend?.Launch(program, args);
    }

    /// <summary>Registers for the initial stop event before launching (avoids missing int3 trap stop).</summary>
    public async Task<bool> LaunchAndWaitForInitialStopAsync(
        string program,
        IEnumerable<ResolvedDebugBreakpoint> breakpoints,
        TimeSpan timeout,
        string[]? args = null,
        string? nasmPath = null,
        SourceMapDocument? sourceMap = null)
    {
        if (_directBackend == null)
            return false;

        LastStopInfo = null;
        var backend = _directBackend;
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnStopped(DebugStopInfo info)
        {
            LastStopInfo = info;
            tcs.TrySetResult(true);
        }

        backend.Stopped += OnStopped;
        try
        {
            await Task.Run(() =>
            {
                DebugBackendFactory.PrepareExecutable(_directBackend!, program, nasmPath, sourceMap);
                ApplyResolvedBreakpoints(breakpoints);
                LaunchDirect(program, args);
            }).ConfigureAwait(true);

            if (tcs.Task.IsCompleted)
                return tcs.Task.GetAwaiter().GetResult();

            using var cts = new CancellationTokenSource(timeout);
            await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            return IsLikelyEntryPause(LastStopInfo);
        }
        finally
        {
            backend.Stopped -= OnStopped;
        }
    }

    private static bool IsLikelyEntryPause(DebugStopInfo? info)
    {
        if (info == null || DebugStopClassifier.IsProgramEnded(info))
            return false;

        var frame = info.Frames.FirstOrDefault();
        return frame != null;
    }

    public void ContinueDirect()
    {
        LogUserAction("Continue");
        _directBackend?.Continue();
    }

    public void StepOverDirect()
    {
        LogUserAction("Step Over");
        _directBackend?.StepOver();
    }

    public void StepIntoDirect()
    {
        LogUserAction("Step Into");
        _directBackend?.StepInto();
    }

    public void StepOutDirect()
    {
        LogUserAction("Step Out");
        _directBackend?.StepOut();
    }

    public void KillDirect()
    {
        LogUserAction("Stop Debug");
        _directBackend?.Kill();
    }

    public bool IsDirectBackendAlive => _directBackend?.IsEngineAlive == true;

    public async Task<bool> WaitForStopAsync(TimeSpan timeout)
    {
        var backend = _directBackend;
        if (backend == null)
            return false;

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnStopped(DebugStopInfo _) => tcs.TrySetResult(true);
        backend.Stopped += OnStopped;
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            await tcs.Task.WaitAsync(cts.Token).ConfigureAwait(true);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            backend.Stopped -= OnStopped;
        }
    }

    public void QueryDebugStateDirect()
    {
        if (_directBackend == null || !_directBackend.IsEngineAlive)
        {
            MessageReceived?.Invoke("(debugger session ended — target ran to completion or debugger exited)");
            return;
        }

        if (DebugStopClassifier.IsProgramEnded(LastStopInfo))
            return;

        try
        {
            var frame = _directBackend.GetStackFrames().FirstOrDefault();
            if (frame != null)
                MessageReceived?.Invoke($"  frame: {frame}");

            if (_directBackend.IsEngineAlive)
            {
                foreach (var reg in _directBackend.GetRegisters().Take(6))
                    MessageReceived?.Invoke($"  {reg.Name} = {reg.Value}");
            }
        }
        catch (IOException ex)
        {
            MessageReceived?.Invoke($"Debug query failed: {ex.Message}");
        }
    }

    public void StartCliProcess(string? repoRoot = null)
    {
        Stop();
        if (!TryStartBundledCli(out var started) && !TryStartDotnetCli(repoRoot, out started))
        {
            MessageReceived?.Invoke("Debug: CLI not found; use direct backend instead.");
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

    public void SendStackTrace(int threadId = 1)
        => SendRequest("stackTrace", new { threadId });

    public void SendScopes(int frameId = 1)
        => SendRequest("scopes", new { frameId });

    public void SendRegisterVariables(int variablesReference = 2)
        => SendRequest("variables", new { variablesReference });

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

        try { _directBackend?.Kill(); } catch { /* ignore */ }
        try { _directBackend?.Disconnect(); } catch { /* ignore */ }
        _directBackend?.Dispose();
        _directBackend = null;
        LastStopInfo = null;
        _seq = 1;

        if (OperatingSystem.IsWindows())
            DebugProcessCleanup.ReleaseDebuggerProcesses();
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
                MessageReceived?.Invoke(FormatDapLine(line));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            MessageReceived?.Invoke($"DAP read error: {ex.Message}");
        }
    }

    private static string FormatDapLine(string line)
    {
        try
        {
            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            if (root.TryGetProperty("type", out var typeEl))
            {
                var type = typeEl.GetString();
                if (type == "event" && root.TryGetProperty("event", out var ev))
                {
                    var name = ev.GetString() ?? "event";
                    if (name == "stopped" && root.TryGetProperty("body", out var body))
                        return $"← EVENT stopped reason={FormatJsonProp(body, "reason")} thread={FormatJsonProp(body, "threadId")}";
                    if (name == "output" && root.TryGetProperty("body", out var outBody) &&
                        outBody.TryGetProperty("output", out var outText))
                        return $"← OUTPUT {outText.GetString()?.TrimEnd()}";
                    return $"← EVENT {name} {root.GetProperty("body").GetRawText()}";
                }

                if (type == "response")
                {
                    var cmd = root.TryGetProperty("command", out var c) ? c.GetString() : "?";
                    var ok = root.TryGetProperty("success", out var s) && s.GetBoolean();
                    if (cmd == "stackTrace" && ok && root.TryGetProperty("body", out var stBody) &&
                        stBody.TryGetProperty("stackFrames", out var frames))
                        return $"← stackTrace ({frames.GetArrayLength()} frames)";
                    if (cmd == "variables" && ok && root.TryGetProperty("body", out var varBody) &&
                        varBody.TryGetProperty("variables", out var vars))
                        return $"← registers/variables ({vars.GetArrayLength()} items)";
                    return ok ? $"← OK {cmd}" : $"← ERR {cmd}: {FormatJsonProp(root, "message")}";
                }
            }
        }
        catch { /* not JSON */ }

        return $"← {line}";
    }

    private static string FormatJsonProp(JsonElement parent, string name)
        => parent.TryGetProperty(name, out var el) ? el.ToString() : "?";

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
