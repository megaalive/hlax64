using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class DebugCapabilityProbeTests
{
    [Fact]
    public void IsDefaultBackendAvailable_matches_platform()
    {
        if (OperatingSystem.IsWindows())
            Assert.True(DebugBackendFactory.IsDefaultBackendAvailable());
        else if (OperatingSystem.IsLinux())
            Assert.Equal(DebugCapabilityProbe.ProbeFast().GdbAvailable,
                DebugBackendFactory.IsDefaultBackendAvailable());
    }

    [Fact]
    public void ProbeFast_returns_structured_report()
    {
        var report = DebugCapabilityProbe.ProbeFast();

        Assert.False(string.IsNullOrWhiteSpace(report.Platform));
        Assert.False(string.IsNullOrWhiteSpace(report.Summary));
        Assert.Equal(OperatingSystem.IsWindows(), report.WindowsEntryTrapSupported);

        if (report.GdbAvailable)
            Assert.False(string.IsNullOrWhiteSpace(report.GdbPath));
    }

    [Fact]
    public void Probe_without_smoke_does_not_require_executable()
    {
        var report = DebugCapabilityProbe.Probe(runSmokeTest: false);
        Assert.False(string.IsNullOrWhiteSpace(report.Summary));
    }
}
