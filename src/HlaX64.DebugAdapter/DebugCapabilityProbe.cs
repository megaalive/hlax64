using System.Globalization;

namespace HlaX64.DebugAdapter;

public sealed record DebugCapabilityReport(
    string Platform,
    bool GdbAvailable,
    string? GdbPath,
    bool LldbUsable,
    string? LldbUnavailableReason,
    bool WindowsEntryTrapSupported,
    string Summary);

public sealed record DebugSteppingSmokeResult(
    bool Launched,
    bool InitialStop,
    bool SteppingOk,
    int StepsCompleted,
    IReadOnlyList<ulong> Rips,
    bool OutputWritableAfterKill,
    string? FailureReason);

/// <summary>Fast debugger capability probes and optional headless stepping smoke tests.</summary>
public static class DebugCapabilityProbe
{
    public static DebugCapabilityReport ProbeFast()
    {
        var gdbAvailable = DebuggerProbe.TryFindGdb(out var gdbPath);
        var lldbUsable = DebuggerProbe.IsLldbUsable(out var lldbReason);
        var entryTrap = OperatingSystem.IsWindows();

        var summary = BuildSummary(gdbAvailable, lldbUsable, entryTrap);

        return new DebugCapabilityReport(
            Platform: OperatingSystem.IsWindows() ? "windows"
                : OperatingSystem.IsLinux() ? "linux"
                : Environment.OSVersion.Platform.ToString(),
            GdbAvailable: gdbAvailable,
            GdbPath: gdbAvailable ? gdbPath : null,
            LldbUsable: lldbUsable,
            LldbUnavailableReason: lldbUsable ? null : lldbReason,
            WindowsEntryTrapSupported: entryTrap,
            Summary: summary);
    }

    public static DebugCapabilityReport Probe(bool runSmokeTest, string? executableForSmoke = null)
    {
        var fast = ProbeFast();
        if (!runSmokeTest || string.IsNullOrWhiteSpace(executableForSmoke))
            return fast;

        var smoke = RunSteppingSmokeAsync(executableForSmoke).GetAwaiter().GetResult();
        var summary = smoke.SteppingOk
            ? "stepping smoke OK"
            : !string.IsNullOrWhiteSpace(smoke.FailureReason)
                ? $"stepping smoke failed: {smoke.FailureReason}"
                : smoke.InitialStop
                    ? $"stepping smoke failed: {smoke.FailureReason ?? "unknown"}"
                    : fast.Summary;

        return fast with { Summary = summary };
    }

    public static async Task<DebugSteppingSmokeResult> RunSteppingSmokeAsync(
        string executablePath,
        int stepCount = 5,
        TimeSpan? launchTimeout = null,
        TimeSpan? stepTimeout = null,
        CancellationToken cancellationToken = default)
    {
        launchTimeout ??= TimeSpan.FromSeconds(20);
        stepTimeout ??= TimeSpan.FromSeconds(12);

        if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return new DebugSteppingSmokeResult(
                false, false, false, 0, [], false, "executable not found");
        }

        if (!DebuggerProbe.TryFindGdb(out _) && !OperatingSystem.IsWindows())
        {
            return new DebugSteppingSmokeResult(
                false, false, false, 0, [], false, "gdb not available");
        }

        using var backend = DebugBackendFactory.CreateDefault();
        if (!backend.IsAvailable)
        {
            return new DebugSteppingSmokeResult(
                false, false, false, 0, [], false,
                DebugBackendFactory.GetUnavailableReason() ?? "debugger not available");
        }

        DebugBackendFactory.PrepareExecutable(backend, executablePath);

        var launched = false;
        var initialStop = false;
        var stepsCompleted = 0;
        var rips = new List<ulong>();
        string? failureReason = null;
        var writableAfterKill = false;
        DebugStopInfo? lastStop = null;

        try
        {
            var launchTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            void OnStopped(DebugStopInfo info)
            {
                lastStop = info;
                launchTcs.TrySetResult(true);
            }

            backend.Stopped += OnStopped;
            try
            {
                await Task.Run(() =>
                {
                    backend.SetBreakpointBySymbol("_start");
                    backend.Launch(executablePath);
                    launched = true;
                }, cancellationToken).ConfigureAwait(false);

                if (!backend.IsEngineAlive && lastStop == null)
                {
                    failureReason =
                        "debugger exited before initial stop (Windows GDB stepping is unreliable without entry trap)";
                }
                else
                {
                    try
                    {
                        using var launchCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                        launchCts.CancelAfter(launchTimeout.Value);
                        await launchTcs.Task.WaitAsync(launchCts.Token).ConfigureAwait(false);
                        initialStop = true;
                    }
                    catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                    {
                        initialStop = IsLikelyEntryPause(lastStop) && backend.IsEngineAlive;
                    }

                    if (!initialStop)
                    {
                        failureReason = lastStop == null
                            ? "timed out waiting for initial stop"
                            : DebugStopClassifier.IsProgramEnded(lastStop)
                                ? "program ended before breakpoint"
                                : "no initial stop event";
                    }
                    else if (DebugStopClassifier.IsProgramEnded(lastStop))
                    {
                        initialStop = false;
                        failureReason = "program ended at initial stop";
                    }
                    else
                    {
                        RecordRip(backend, lastStop, rips);

                        for (var step = 0; step < stepCount && failureReason == null; step++)
                        {
                            if (DebugStopClassifier.IsProgramEnded(lastStop))
                                break;

                            var stepTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                            void OnStepStop(DebugStopInfo info)
                            {
                                lastStop = info;
                                stepTcs.TrySetResult(true);
                            }

                            backend.Stopped += OnStepStop;
                            try
                            {
                                backend.StepOver();
                                using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                                stepCts.CancelAfter(stepTimeout.Value);
                                await stepTcs.Task.WaitAsync(stepCts.Token).ConfigureAwait(false);
                                stepsCompleted++;
                                RecordRip(backend, lastStop, rips);
                            }
                            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                            {
                                failureReason = $"timed out waiting for step {step + 1}";
                            }
                            finally
                            {
                                backend.Stopped -= OnStepStop;
                            }

                            if (DebugStopClassifier.IsProgramEnded(lastStop))
                                break;
                        }
                    }
                }
            }
            finally
            {
                backend.Stopped -= OnStopped;
            }
        }
        finally
        {
            try
            {
                if (backend.IsEngineAlive)
                {
                    backend.Kill();
                    backend.Disconnect();
                }
            }
            catch
            {
                // best-effort cleanup for CI
            }

            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            ReleaseDebugArtifacts(executablePath);
            writableAfterKill = DebugProcessCleanup.TryEnsureWritable(executablePath, timeoutMs: 8000);
        }

        var steppingOk = failureReason == null
                         && stepsCompleted >= stepCount
                         && rips.Count >= 2
                         && rips.Distinct().Count() >= 2;

        if (failureReason == null && !steppingOk)
        {
            failureReason = stepsCompleted < stepCount
                ? $"completed {stepsCompleted}/{stepCount} steps before stop"
                : rips.Distinct().Count() < 2
                    ? "RIP did not advance across steps"
                    : "stepping smoke failed";
        }

        return new DebugSteppingSmokeResult(
            launched,
            initialStop,
            steppingOk,
            stepsCompleted,
            rips,
            writableAfterKill,
            steppingOk ? null : failureReason);
    }

    private static void ReleaseDebugArtifacts(string executablePath)
    {
        DebugProcessCleanup.ReleaseOutputFile(executablePath);
        var dir = Path.GetDirectoryName(executablePath);
        var baseName = Path.GetFileNameWithoutExtension(executablePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(baseName))
            return;

        var shadow = Path.Combine(dir, $"{baseName}-labdbg.exe");
        DebugProcessCleanup.ReleaseOutputFile(shadow);
        DebugShadowExecutable.TryDelete(shadow);
        DebugProcessCleanup.ReleaseDebuggerProcesses();
    }

    private static void RecordRip(IDebugBackend backend, DebugStopInfo? stop, List<ulong> rips)
    {
        var rip = ParseAddress(stop?.Frames.FirstOrDefault()?.Address)
                  ?? DebugBackendFactory.TryGetCurrentRip(backend);
        if (rip != null)
            rips.Add(rip.Value);
    }

    private static bool IsLikelyEntryPause(DebugStopInfo? info)
    {
        if (info == null || DebugStopClassifier.IsProgramEnded(info))
            return false;

        return info.Frames.Count > 0 || info.Reason is "breakpoint-hit" or "signal-received";
    }

    private static ulong? ParseAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return ulong.TryParse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var addr)
            ? addr
            : null;
    }

    private static string BuildSummary(bool gdbAvailable, bool lldbUsable, bool entryTrap)
    {
        if (OperatingSystem.IsWindows() && Win32DebugBackend.IsSupported)
            return "Win32 native debugger available";

        if (!gdbAvailable && !lldbUsable)
            return "no native debugger on PATH";

        if (OperatingSystem.IsWindows())
        {
            if (gdbAvailable)
                return "GDB on Windows (stepping unreliable until Win32 backend)";
            if (lldbUsable)
                return "LLDB available on Windows";
        }

        if (OperatingSystem.IsLinux() && gdbAvailable)
            return "GDB on Linux";

        if (gdbAvailable)
            return "GDB available";

        return entryTrap ? "LLDB available" : "debugger probe incomplete";
    }
}
