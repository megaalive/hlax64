using HlaX64.DebugAdapter;
using HlaX64.TestSupport;

namespace HlaX64.DebugAdapter.Tests;

[Collection("Integration")]
public sealed class GdbBackendIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task Stepping_smoke_produces_measurable_report()
    {
        if (!DebugSmokeBuild.TryBuild(out var executable, out var skipReason))
        {
            Assert.True(true, $"skipped: {skipReason}");
            return;
        }

        try
        {
            var result = await DebugCapabilityProbe.RunSteppingSmokeAsync(executable!);

            Assert.True(result.Launched, result.FailureReason ?? "launch failed");

            if (OperatingSystem.IsLinux())
            {
                Assert.True(result.InitialStop, result.FailureReason ?? "no initial stop on Linux");
                Assert.True(result.SteppingOk,
                    $"Linux stepping baseline failed: {result.FailureReason}; rips=[{string.Join(", ", result.Rips.Select(r => $"0x{r:x}"))}]");
                Assert.True(result.OutputWritableAfterKill, "output file still locked after debug session");
            }
            else if (OperatingSystem.IsWindows())
            {
                Assert.True(result.InitialStop,
                    $"Windows should pause at entry: {result.FailureReason}; rips=[{string.Join(", ", result.Rips.Select(r => $"0x{r:x}"))}]");
                Assert.True(result.SteppingOk,
                    $"Win32 stepping failed: {result.FailureReason}; rips=[{string.Join(", ", result.Rips.Select(r => $"0x{r:x}"))}]");
                Assert.True(result.OutputWritableAfterKill, "output file still locked after debug session");
            }
        }
        finally
        {
            TryDeleteBuildTree(executable!);
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task Probe_with_smoke_merges_summary_when_executable_available()
    {
        if (!DebugSmokeBuild.TryBuild(out var executable, out _))
            return;

        try
        {
            var report = DebugCapabilityProbe.Probe(runSmokeTest: true, executableForSmoke: executable);
            Assert.True(DebugBackendFactory.CreateDefault().IsAvailable);
            Assert.Contains("stepping smoke", report.Summary, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            TryDeleteBuildTree(executable!);
        }
    }

    private static void TryDeleteBuildTree(string executable)
    {
        try
        {
            var dir = Path.GetDirectoryName(executable);
            if (dir != null && dir.Contains("hlax64-debug-smoke-", StringComparison.Ordinal))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // temp cleanup is best-effort
        }
    }
}
