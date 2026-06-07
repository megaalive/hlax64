using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class LinuxRealToolTests
{
    [Fact]
    public void LinuxRealTool_linecount_runs_under_wsl_when_available()
    {
        if (!LinkerTool.TryFindLinker(out var linker, out _, out _))
            return;
        if (!linker.Equals("wsl", StringComparison.OrdinalIgnoreCase))
            return;
        if (!NasmTool.TryFindNasm(out _))
            return;

        var repoRoot = FindRepoRoot();
        var toolDir = Path.Combine(repoRoot, "examples", "12-real-tools-linux", "linecount");
        var sourcePath = Path.Combine(toolDir, "linecount.hla64");
        var expectedStdoutPath = Path.Combine(toolDir, "expected.stdout");
        var expectedExitPath = Path.Combine(toolDir, "expected.exitcode");
        var argumentsPath = Path.Combine(toolDir, "expected.arguments");
        if (!File.Exists(sourcePath) || !File.Exists(expectedStdoutPath) || !File.Exists(expectedExitPath))
            return;

        var expectedStdout = File.ReadAllText(expectedStdoutPath).Replace("\r\n", "\n").TrimEnd('\n');
        var expectedExit = int.Parse(File.ReadAllText(expectedExitPath).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        var arg = File.Exists(argumentsPath) ? File.ReadAllText(argumentsPath).Trim() : null;
        var fixtureArg = arg != null && !Path.IsPathRooted(arg)
            ? Path.Combine(repoRoot, arg.Replace('\\', '/'))
            : arg;

        var options = CompilationOptions.Default with { Target = TargetTriple.LinuxX64SysV };
        var cache = Path.Combine(Path.GetTempPath(), "hlax64-linux-linecount-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var artifacts = CompilePipeline.Compile(sourcePath, File.ReadAllText(sourcePath), options);
            Directory.CreateDirectory(cache);
            var nasmFile = Path.Combine(cache, "linecount.nasm");
            var objFile = Path.Combine(cache, "linecount.o");
            var exeFile = Path.Combine(cache, "linecount");
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            var nasmTool = new NasmTool(NasmTool.TryFindNasm(out var nasmPath) ? nasmPath : null);
            Assert.True(nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: "elf64"), nasmError);
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                artifacts.Result, isWindows: false, cache, out var extras, out var runtimeError), runtimeError);
            Assert.True(LinkerTool.TryLink(objFile, exeFile, out var linkError, out _, extraLibraries: extras), linkError);

            var wslExe = LinkerTool.ToWslPath(exeFile);
            var wslFixture = fixtureArg != null ? LinkerTool.ToWslPath(fixtureArg) : null;
            var wslCwd = LinkerTool.ToWslPath(repoRoot);
            var args = wslFixture != null ? $"{wslExe} {wslFixture}" : wslExe;

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"bash -lc \"cd '{wslCwd}' && {args}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.NotNull(process);
            var stdout = process!.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(20000), "linux linecount did not exit within 20s\n" + stderr);

            Assert.Equal(expectedExit, process.ExitCode);
            foreach (var line in expectedStdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                Assert.Contains(line, stdout, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
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
