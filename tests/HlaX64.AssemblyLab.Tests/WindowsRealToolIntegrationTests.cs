using HlaX64.AssemblyLab.Services;
using HlaX64.Cli.Toolchain;
using HlaX64.TestSupport;

namespace HlaX64.AssemblyLab.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class WindowsRealToolIntegrationTests
{
    public WindowsRealToolIntegrationTests()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        Environment.SetEnvironmentVariable("HLAX64_RUNTIME_DIR",
            Path.Combine(repoRoot, "src", "HlaX64.Runtime"));
    }

    public static IEnumerable<object[]> RealToolCases()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var toolsRoot = Path.Combine(repoRoot, "examples", "tools", "10-windows");
        if (!Directory.Exists(toolsRoot))
            yield break;

        foreach (var toolDir in Directory.GetDirectories(toolsRoot).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            var tool = Path.GetFileName(toolDir);
            if (tool.StartsWith('_'))
                continue;

            var sourcePath = Path.Combine(toolDir, $"{tool}.hla64");
            var expectedStdoutPath = Path.Combine(toolDir, "expected.stdout");
            var expectedExitPath = Path.Combine(toolDir, "expected.exitcode");
            if (!File.Exists(sourcePath) || !File.Exists(expectedStdoutPath) || !File.Exists(expectedExitPath))
                continue;

            var expectedStdout = File.ReadAllText(expectedStdoutPath).Replace("\r\n", "\n").TrimEnd('\n');
            var expectedExit = int.Parse(File.ReadAllText(expectedExitPath).Trim(), System.Globalization.CultureInfo.InvariantCulture);
            yield return new object[] { tool, expectedStdout, expectedExit };
        }
    }

    [Theory]
    [MemberData(nameof(RealToolCases))]
    public void RealTool_builds_and_runs_natively_on_windows(string tool, string expectedStdout, int expectedExit)
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!LinkerTool.TryFindWindowsLinker(out _, out _, out _))
            return;

        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", tool, $"{tool}.hla64");
        if (!File.Exists(sourcePath))
            return;

        var backend = new AssemblyLabBackend();
        var outDir = Path.Combine(Path.GetTempPath(), $"hlax64-real-{tool}-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var build = backend.Build(sourcePath, File.ReadAllText(sourcePath), "windows-x64-msabi", outDir);
            Assert.True(build.Success, build.Message);
            Assert.NotNull(build.OutputFile);
            Assert.True(File.Exists(build.OutputFile!), build.Message);

            var toolDir = Path.Combine(repoRoot, "examples", "tools", "10-windows", tool);
            var argumentsPath = Path.Combine(toolDir, "expected.arguments");
            var stdinPath = Path.Combine(toolDir, "expected.stdin");
            var expectedOutputPath = Path.Combine(toolDir, "expected.output");
            var expectedOutput = File.Exists(expectedOutputPath)
                ? File.ReadAllText(expectedOutputPath).Replace("\r\n", "\n")
                : null;
            var outputFile = Path.Combine(outDir, $"{tool}-out.txt");
            using var tcpFixture = LocalTcpFixture.TryStart(toolDir);
            var arguments = RealToolTestHarness.BuildWindowsArguments(argumentsPath, repoRoot, outputFile, tcpFixture?.Port ?? 0);

            var stdin = File.Exists(stdinPath)
                ? File.ReadAllText(stdinPath).Replace("\r\n", "\n")
                : null;
            var result = ProcessRunner.Run(new System.Diagnostics.ProcessStartInfo
            {
                FileName = build.OutputFile!,
                Arguments = arguments,
                WorkingDirectory = repoRoot
            }, TimeSpan.FromSeconds(15), stdin);

            Assert.False(result.TimedOut, $"{tool} timed out\n{result.Stderr}");
            Assert.True(result.ExitCode == expectedExit,
                $"exit: {result.ExitCode}, expected: {expectedExit}\nstdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
            foreach (var line in expectedStdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Assert.Contains(line, result.Stdout, StringComparison.Ordinal);

            if (expectedOutput != null)
            {
                Assert.True(File.Exists(outputFile), $"expected output file missing for {tool}");
                var actualOutput = File.ReadAllText(outputFile).Replace("\r\n", "\n");
                Assert.Equal(expectedOutput, actualOutput);
            }

            tcpFixture?.WaitForCompletion();
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }
}
