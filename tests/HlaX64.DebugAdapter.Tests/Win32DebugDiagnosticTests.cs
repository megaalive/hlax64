using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class Win32DebugDiagnosticTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Win32_launch_emits_debug_events()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!DebugSmokeBuild.TryBuild(out var executable, out var skipReason))
        {
            Assert.Fail($"skip build: {skipReason}");
            return;
        }

        var lines = new List<string>();
        using var backend = new Win32DebugBackend();
        backend.OutputReceived += lines.Add;
        var stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.Stopped += info =>
        {
            lines.Add($"STOP reason={info.Reason} rip={info.Frames.FirstOrDefault()?.Address}");
            stopped.TrySetResult(true);
        };

        backend.PrepareExecutable(executable!);
        backend.SetBreakpointBySymbol("_start");
        backend.Launch(executable!);

        var gotStop = await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(12))) == stopped.Task;
        backend.Kill();
        backend.Disconnect();
        await Task.Delay(500);

        Assert.True(gotStop, string.Join(Environment.NewLine, lines));
        Assert.Contains(lines, line => line.StartsWith("STOP reason=breakpoint-hit", StringComparison.OrdinalIgnoreCase)
                                       || line.StartsWith("STOP reason=step", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Win32_dnslookup_launch_reaches_breakpoint_without_access_violation()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        if (!File.Exists(exe))
            return;

        var lines = new List<string>();
        using var backend = new Win32DebugBackend();
        backend.OutputReceived += lines.Add;
        var stopped = new TaskCompletionSource<DebugStopInfo>(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.Stopped += info =>
        {
            lines.Add($"STOP reason={info.Reason} rip={info.Frames.FirstOrDefault()?.Address}");
            stopped.TrySetResult(info);
        };

        backend.PrepareExecutable(exe);
        backend.SetBreakpointBySymbol("_start");
        backend.Launch(exe);

        var completed = await Task.WhenAny(stopped.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        backend.Kill();
        backend.Disconnect();
        await Task.Delay(500);

        Assert.True(completed == stopped.Task, string.Join(Environment.NewLine, lines));
        var info = await stopped.Task;
        Assert.DoesNotContain(lines, l => l.Contains("access violation", StringComparison.OrdinalIgnoreCase));
        Assert.False(string.Equals(info.Reason, "exited-signalled", StringComparison.Ordinal));
        Assert.True(info.Reason is "breakpoint-hit" or "step",
            $"expected pause, got {info.Reason}; log={string.Join(" | ", lines)}");
    }
}
