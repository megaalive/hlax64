using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using HlaX64.TestSupport;

namespace HlaX64.Compiler.Tests;

[Collection("Integration")]
[Trait("Category", "Integration")]
public sealed class LinuxRealToolTests
{
    public LinuxRealToolTests()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        Environment.SetEnvironmentVariable("HLAX64_RUNTIME_DIR",
            Path.Combine(repoRoot, "src", "HlaX64.Runtime"));
    }

    public static IEnumerable<object[]> LinuxRealToolCases()
    {
        yield return ["linecount"];
        yield return ["exists"];
        yield return ["wc"];
        yield return ["fnv1a"];
        yield return ["cat"];
        yield return ["strings"];
        yield return ["cp"];
        yield return ["tee"];
        yield return ["head"];
        yield return ["nl"];
        yield return ["grep"];
        yield return ["cmp"];
        yield return ["hexdump"];
        yield return ["filemagic"];
        yield return ["pid"];
        yield return ["hostname"];
        yield return ["uptime"];
        yield return ["meminfo"];
        yield return ["filesize"];
        yield return ["machine"];
        yield return ["netcheck"];
        yield return ["tcpget"];
        yield return ["httpget"];
        yield return ["dnslookup"];
        yield return ["cpucount"];
        yield return ["diskfree"];
        yield return ["procmem"];
        yield return ["loadavg"];
        yield return ["machine2"];
    }

    [Theory]
    [MemberData(nameof(LinuxRealToolCases))]
    public void LinuxRealTool_runs_under_wsl_when_available(string tool)
    {
        if (!LinkerTool.TryFindLinker(out var linker, out _, out _))
            return;
        if (!linker.Equals("wsl", StringComparison.OrdinalIgnoreCase))
            return;
        if (!NasmTool.TryFindNasm(out _))
            return;

        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var toolDir = Path.Combine(repoRoot, "examples", "tools", "12-linux", tool);
        var sourcePath = Path.Combine(toolDir, $"{tool}.hla64");
        var expectedStdoutPath = Path.Combine(toolDir, "expected.stdout");
        var expectedExitPath = Path.Combine(toolDir, "expected.exitcode");
        var argumentsPath = Path.Combine(toolDir, "expected.arguments");
        if (!File.Exists(sourcePath) || !File.Exists(expectedStdoutPath) || !File.Exists(expectedExitPath))
            return;

        var expectedStdout = File.ReadAllText(expectedStdoutPath).Replace("\r\n", "\n").TrimEnd('\n');
        var expectedExit = int.Parse(File.ReadAllText(expectedExitPath).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        var stdinPath = Path.Combine(toolDir, "expected.stdin");
        var expectedOutputPath = Path.Combine(toolDir, "expected.output");
        var expectedOutput = File.Exists(expectedOutputPath)
            ? File.ReadAllText(expectedOutputPath).Replace("\r\n", "\n")
            : null;

        var options = CompilationOptions.Default with { Target = TargetTriple.LinuxX64SysV };
        var cache = Path.Combine(Path.GetTempPath(), $"hlax64-linux-{tool}-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var artifacts = CompilePipeline.Compile(sourcePath, File.ReadAllText(sourcePath), options);
            Directory.CreateDirectory(cache);
            var nasmFile = Path.Combine(cache, $"{tool}.nasm");
            var objFile = Path.Combine(cache, $"{tool}.o");
            var exeFile = Path.Combine(cache, tool);
            var outputFile = Path.Combine(cache, $"{tool}-out.txt");
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            var nasmTool = new NasmTool(NasmTool.TryFindNasm(out var nasmPath) ? nasmPath : null);
            Assert.True(nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: "elf64"), nasmError);
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                artifacts.Result, isWindows: false, cache, out var extras, out var runtimeError), runtimeError);
            Assert.True(LinkerTool.TryLink(objFile, exeFile, out var linkError, out _, extraLibraries: extras), linkError);

            using var tcpFixture = LocalTcpFixture.TryStart(toolDir);
            var wslExe = LinkerTool.ToWslPath(exeFile);
            var wslCwd = LinkerTool.ToWslPath(repoRoot);
            var wslHostIp = WslHostResolver.TryGetHostIpForWsl();
            var args = RealToolTestHarness.BuildWslArguments(argumentsPath, repoRoot, outputFile, tcpFixture?.Port ?? 0, wslHostIp);

            string command;
            if (File.Exists(stdinPath))
            {
                var stdinFile = Path.Combine(cache, "stdin.txt");
                File.WriteAllText(stdinFile, File.ReadAllText(stdinPath).Replace("\r\n", "\n"));
                var wslStdin = LinkerTool.ToWslPath(stdinFile);
                command = $"bash -lc \"cd '{wslCwd}' && cat '{wslStdin}' | '{wslExe}' {args}\"";
            }
            else
            {
                command = string.IsNullOrWhiteSpace(args)
                    ? $"bash -lc \"cd '{wslCwd}' && '{wslExe}'\""
                    : $"bash -lc \"cd '{wslCwd}' && '{wslExe}' {args}\"";
            }

            var result = ProcessRunner.Run(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = command
            }, TimeSpan.FromSeconds(20));

            Assert.False(result.TimedOut, $"linux {tool} did not exit within 20s\n{result.Stderr}");
            Assert.Equal(expectedExit, result.ExitCode);
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
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }
}

