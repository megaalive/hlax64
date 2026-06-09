using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class LinuxRealToolTests
{
    public LinuxRealToolTests()
    {
        var repoRoot = FindRepoRoot();
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

        var repoRoot = FindRepoRoot();
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

            var wslExe = LinkerTool.ToWslPath(exeFile);
            var wslCwd = LinkerTool.ToWslPath(repoRoot);
            var args = BuildWslArguments(argumentsPath, repoRoot, outputFile);

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

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = command,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.NotNull(process);
            var stdout = process!.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(20000), $"linux {tool} did not exit within 20s\n{stderr}");

            Assert.Equal(expectedExit, process.ExitCode);
            foreach (var line in expectedStdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Assert.Contains(line, stdout, StringComparison.Ordinal);

            if (expectedOutput != null)
            {
                Assert.True(File.Exists(outputFile), $"expected output file missing for {tool}");
                var actualOutput = File.ReadAllText(outputFile).Replace("\r\n", "\n");
                Assert.Equal(expectedOutput, actualOutput);
            }
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }

    private static string BuildWslArguments(string? argumentsPath, string repoRoot, string outputFile)
    {
        if (argumentsPath == null || !File.Exists(argumentsPath))
            return string.Empty;

        var raw = File.ReadAllText(argumentsPath);
        var tokens = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return string.Empty;

        var resolved = new List<string>();
        foreach (var token in tokens)
        {
            if (token == "$OUTPUT")
                resolved.Add(LinkerTool.ToWslPath(outputFile));
            else if (!Path.IsPathRooted(token))
                resolved.Add(LinkerTool.ToWslPath(Path.Combine(repoRoot, token.Replace('\\', '/'))));
            else
                resolved.Add(LinkerTool.ToWslPath(token));
        }

        return string.Join(' ', resolved.Select(arg => $"'{arg.Replace("'", "'\\''")}'"));
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HlaX64.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repo root");
    }
}
