using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using HlaX64.TestSupport;

namespace HlaX64.Compiler.Tests;

public sealed class WslLocalTcpFixtureTests
{
    [Fact]
    public void WslLocalTcpFixture_runs_linux_curl_against_in_wsl_server()
    {
        if (!OperatingSystem.IsWindows())
            return;
        if (!LinkerTool.TryFindLinker(out var linker, out _, out _))
            return;
        if (!linker.Equals("wsl", StringComparison.OrdinalIgnoreCase))
            return;
        if (!NasmTool.TryFindNasm(out _))
            return;

        var repoRoot = RealToolTestHarness.FindRepoRoot();
        Environment.SetEnvironmentVariable("HLAX64_RUNTIME_DIR",
            Path.Combine(repoRoot, "src", "HlaX64.Runtime"));

        var toolDir = Path.Combine(repoRoot, "examples", "tools", "12-linux", "curl");
        var sourcePath = Path.Combine(toolDir, "curl.hla64");
        var cache = Path.Combine(Path.GetTempPath(), "hlax-wsl-fixture-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var artifacts = CompilePipeline.Compile(sourcePath, File.ReadAllText(sourcePath),
                CompilationOptions.Default with { Target = TargetTriple.LinuxX64SysV });
            Directory.CreateDirectory(cache);
            var nasmFile = Path.Combine(cache, "curl.nasm");
            var objFile = Path.Combine(cache, "curl.o");
            var exeFile = Path.Combine(cache, "curl");
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            var nasmTool = new NasmTool(NasmTool.TryFindNasm(out var nasmPath) ? nasmPath : null);
            Assert.True(nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: "elf64"), nasmError);
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                artifacts.Result, isWindows: false, cache, out var extras, out var runtimeError), runtimeError);
            Assert.True(LinkerTool.TryLink(objFile, exeFile, out var linkError, out _, extraLibraries: extras), linkError);

            using var fixture = WslLocalTcpFixture.TryStart(toolDir);
            Assert.NotNull(fixture);
            var outputFile = Path.Combine(cache, "curl-out.txt");
            var expectedOutputPath = Path.Combine(toolDir, "expected.output");
            var expectedOutput = File.Exists(expectedOutputPath)
                ? File.ReadAllText(expectedOutputPath).Replace("\r\n", "\n")
                : null;
            var command = fixture!.BuildCombinedCommand(
                LinkerTool.ToWslPath(exeFile),
                Path.Combine(toolDir, "expected.arguments"),
                repoRoot,
                Path.Combine(cache, "curl-out.txt"),
                LinkerTool.ToWslPath(repoRoot));

            var result = ProcessRunner.Run(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = command
            }, TimeSpan.FromSeconds(20));

            Assert.False(result.TimedOut, result.Stderr);
            Assert.True(result.ExitCode == 0,
                $"exit={result.ExitCode}\nstdout:\n{result.Stdout}\nstderr:\n{result.Stderr}\ncmd:\n{command}");
            if (expectedOutput != null)
            {
                Assert.True(File.Exists(outputFile), $"expected output file missing\nstdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
                var actualOutput = File.ReadAllText(outputFile).Replace("\r\n", "\n");
                Assert.True(actualOutput == expectedOutput,
                    $"output mismatch\nexpected:\n{expectedOutput}\nactual:\n{actualOutput}\nstdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
            }
            else
            {
                Assert.Contains("OK", result.Stdout, StringComparison.Ordinal);
            }
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }
}
