using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class IdivLoopTests
{
    public static IEnumerable<object[]> WindowsIdivCases()
    {
        yield return ["examples/qa/bug-farm/idiv-loop/idiv-loop.hla64", 60];
        yield return ["examples/qa/bug-farm/idiv-loop/mod-only.hla64", 2079];
        yield return ["examples/qa/bug-farm/idiv-loop/static-store.hla64", 362880];
        yield return ["examples/qa/bug-farm/idiv-loop/lehmer-indices.hla64", 26];
    }

    [Theory]
    [MemberData(nameof(WindowsIdivCases))]
    public void WindowsIdivRegression_runs_with_expected_exit(string relativeSource, int expectedExit)
    {
        if (!LinkerTool.TryFindWindowsLinker(out _, out _, out _))
            return;
        if (!NasmTool.TryFindNasm(out _))
            return;

        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, relativeSource.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(sourcePath))
            return;

        var name = Path.GetFileNameWithoutExtension(sourcePath);
        var cache = Path.Combine(Path.GetTempPath(), $"hlax64-idiv-{name}-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var options = CompilationOptions.Default with { Target = TargetTriple.WindowsX64MsAbi };
            var artifacts = CompilePipeline.Compile(sourcePath, File.ReadAllText(sourcePath), options);
            Directory.CreateDirectory(cache);
            var nasmFile = Path.Combine(cache, $"{name}.nasm");
            var objFile = Path.Combine(cache, $"{name}.obj");
            var exeFile = Path.Combine(cache, $"{name}.exe");
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            var nasmTool = new NasmTool(NasmTool.TryFindNasm(out var nasmPath) ? nasmPath : null);
            Assert.True(nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: "win64"), nasmError);
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                artifacts.Result, isWindows: true, cache, out var extras, out var runtimeError), runtimeError);
            Assert.True(LinkerTool.TryLinkWindows(objFile, exeFile, out var linkError, extraLibraries: extras), linkError);

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exeFile,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.NotNull(process);
            Assert.True(process!.WaitForExit(20000), $"{name} did not exit within 20s");
            Assert.Equal(expectedExit, process.ExitCode);
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void Compile_StaticInt64Array_EmitsQwordMemoryOps()
    {
        const string source = """
            program p;
            static
                table: int64[4];
            endstatic;
            begin p;
                mov(362880, table[3]);
                mov(1, r10);
                mov(r10, table[r10]);
                mov(table[3], rbx);
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter()
            .Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);
        Assert.Contains("mov qword [rel table+24]", nasm, StringComparison.Ordinal);
        Assert.Contains("mov rbx, qword [rel table+24]", nasm, StringComparison.Ordinal);
        Assert.Contains("lea r11, [rel table]", nasm, StringComparison.OrdinalIgnoreCase);
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
        throw new DirectoryNotFoundException("Repo root not found");
    }
}
