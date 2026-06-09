using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class RuntimeObjectProviderTests
{
    [Fact]
    public void TryBuildLinkExtras_dynamic_array_heap_includes_heap_runtime()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "curriculum", "05-memory", "dynamic-array-heap.hla64");
        var source = File.ReadAllText(sourcePath);
        var options = CompilationOptions.Default with { Target = TargetTriple.WindowsX64MsAbi };
        var compilation = new Compilation(sourcePath, source, options);
        var result = compilation.Process();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics));

        var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
        Assert.Contains("hlax_realloc", externs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("hlax_free", externs, StringComparer.OrdinalIgnoreCase);

        var cache = Path.Combine(Path.GetTempPath(), "hlax64-heap-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(result, isWindows: true, cache, out var winExtras, out var error), error);
            Assert.Contains(winExtras, e => e.Contains("hlax64-runtime-heap", StringComparison.OrdinalIgnoreCase));

            options = CompilationOptions.Default with { Target = TargetTriple.LinuxX64SysV };
            compilation = new Compilation(sourcePath, source, options);
            result = compilation.Process();
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(result, isWindows: false, cache, out var linuxExtras, out error), error);
            Assert.Contains(linuxExtras, e => e.Contains("hlax64-runtime-heap", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(linuxExtras, e => e.Equals("-lc", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void TryBuildLinkExtras_linecount_includes_argv_runtime()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "linecount", "linecount.hla64");
        var source = File.ReadAllText(sourcePath);
        var options = CompilationOptions.Default with { Target = TargetTriple.WindowsX64MsAbi };
        var compilation = new Compilation(sourcePath, source, options);
        var result = compilation.Process();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics));

        var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
        Assert.Contains("hlax_argv_init", externs, StringComparer.OrdinalIgnoreCase);

        var cache = Path.Combine(Path.GetTempPath(), "hlax64-argv-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(result, isWindows: true, cache, out var extras, out var error), error);
            Assert.Contains(extras, e => e.Contains("hlax64-runtime-argv", StringComparison.OrdinalIgnoreCase));

            // Emit + assemble minimal link smoke (validates shell32.lib resolves on this machine).
            var emitter = new HlaX64.Backend.Nasm.Emitters.NasmEmitter();
            var nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData,
                new HlaX64.Backend.Nasm.Emitters.NasmEmitOptions { IsWindowsTarget = true });
            var nasmFile = Path.Combine(cache, "linecount.nasm");
            var objFile = Path.Combine(cache, "linecount.obj");
            var exeFile = Path.Combine(cache, "linecount.exe");
            File.WriteAllText(nasmFile, nasm);
            Assert.True(NasmTool.TryFindNasm(out var nasmPath));
            var nasmTool = new NasmTool(nasmPath);
            Assert.True(nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: "win64"), nasmError);
            if (LinkerTool.TryFindWindowsLinker(out _, out _, out _))
            {
                Assert.True(LinkerTool.TryLinkWindows(objFile, exeFile, out var linkError, extraLibraries: extras),
                    $"extras=[{string.Join(", ", extras)}]\n{linkError}");
            }
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "HlaX64.slnx")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
