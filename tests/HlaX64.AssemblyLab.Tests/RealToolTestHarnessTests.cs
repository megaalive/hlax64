using HlaX64.TestSupport;

namespace HlaX64.AssemblyLab.Tests;

public sealed class RealToolTestHarnessTests
{
    [Fact]
    public void BuildWindowsArguments_keeps_hostname_literals()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var argsPath = Path.Combine(Path.GetTempPath(), "hlax64-args-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            File.WriteAllText(argsPath, "localhost\nbeta");
            var args = RealToolTestHarness.BuildWindowsArguments(argsPath, repoRoot, "out.txt", 5010);
            Assert.Equal("localhost beta", args);
        }
        finally
        {
            if (File.Exists(argsPath))
                File.Delete(argsPath);
        }
    }

    [Fact]
    public void BuildWindowsArguments_resolves_existing_repo_relative_paths()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var fixture = Path.Combine("examples", "tools", "10-windows", "dnslookup", "expected.arguments");
        var argsPath = Path.Combine(repoRoot, fixture);
        var args = RealToolTestHarness.BuildWindowsArguments(argsPath, repoRoot, "out.txt", 5010);
        Assert.Equal("localhost", args);
    }
}
