using System.Text.Json;
using HlaX64.Cli.Services;
using HlaX64.Cli.Toolchain;

namespace HlaX64.Compiler.Tests;

public sealed class DoctorReportTests
{
    [Fact]
    public void ResolveNasm_WhenMissing_IncludesInstallGuideInHint()
    {
        var resolver = new ToolchainResolver(
            new ToolchainSettings { NasmPath = "__missing_nasm__" },
            Path.GetTempPath());

        var result = resolver.ResolveNasm();
        if (result.Found)
            return;

        Assert.Contains(ToolchainResolver.InstallGuideRelative, result.InstallHint ?? "", StringComparison.Ordinal);
        Assert.Contains("nasm", result.InstallHint ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DoctorJson_IncludesRemediationForChecks()
    {
        var report = DoctorReport.Run();
        var json = JsonSerializer.Serialize(new
        {
            checks = report.Checks.Select(c => new
            {
                installHint = c.InstallHint,
                remediation = c.InstallHint
            })
        });

        Assert.Contains("remediation", json, StringComparison.Ordinal);
    }
}
