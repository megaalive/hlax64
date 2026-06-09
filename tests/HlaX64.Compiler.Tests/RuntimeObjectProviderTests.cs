using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class RuntimeObjectProviderTests
{
    public RuntimeObjectProviderTests()
    {
        var repoRoot = FindRepoRoot();
        Environment.SetEnvironmentVariable("HLAX64_RUNTIME_DIR",
            Path.Combine(repoRoot, "src", "HlaX64.Runtime"));
    }
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

    [Fact]
    public void TryBuildLinkExtras_exists_includes_file_runtime()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "exists", "exists.hla64");
        var source = File.ReadAllText(sourcePath);

        foreach (var target in new[] { TargetTriple.WindowsX64MsAbi, TargetTriple.LinuxX64SysV })
        {
            var options = CompilationOptions.Default with { Target = target };
            var compilation = new Compilation(sourcePath, source, options);
            var result = compilation.Process();
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));

            var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
            Assert.Contains("hlax_path_exists", externs, StringComparer.OrdinalIgnoreCase);

            var cache = Path.Combine(Path.GetTempPath(), "hlax64-file-test-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                    result, isWindows: target == TargetTriple.WindowsX64MsAbi, cache, out var extras, out var error), error);
                Assert.Contains(extras, e => e.Contains("hlax64-runtime-file", StringComparison.OrdinalIgnoreCase));
                if (target == TargetTriple.LinuxX64SysV)
                    Assert.Contains(extras, e => e.Equals("-lc", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(cache))
                    Directory.Delete(cache, recursive: true);
            }
        }
    }

    [Fact]
    public void TryBuildLinkExtras_wc_includes_file_and_mem_runtime()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "wc", "wc.hla64");
        var source = File.ReadAllText(sourcePath);
        var options = CompilationOptions.Default with { Target = TargetTriple.WindowsX64MsAbi };
        var compilation = new Compilation(sourcePath, source, options);
        var result = compilation.Process();

        Assert.True(result.Success, string.Join("; ", result.Diagnostics));

        var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
        Assert.Contains("hlax_file_open_read", externs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("hlax_is_space", externs, StringComparer.OrdinalIgnoreCase);

        var cache = Path.Combine(Path.GetTempPath(), "hlax64-wc-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(result, isWindows: true, cache, out var extras, out var error), error);
            Assert.Contains(extras, e => e.Contains("hlax64-runtime-file", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(extras, e => e.Contains("hlax64-runtime-mem", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            if (Directory.Exists(cache))
                Directory.Delete(cache, recursive: true);
        }
    }

    [Fact]
    public void TryBuildLinkExtras_cat_includes_file_runtime_and_stdout_write()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "cat", "cat.hla64");
        var source = File.ReadAllText(sourcePath);

        foreach (var target in new[] { TargetTriple.WindowsX64MsAbi, TargetTriple.LinuxX64SysV })
        {
            var options = CompilationOptions.Default with { Target = target };
            var result = new Compilation(sourcePath, source, options).Process();
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));

            var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
            Assert.Contains("hlax_file_open_read", externs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hlax_stdout_write", externs, StringComparer.OrdinalIgnoreCase);

            var cache = Path.Combine(Path.GetTempPath(), "hlax64-cat-test-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                    result, isWindows: target == TargetTriple.WindowsX64MsAbi, cache, out var extras, out var error), error);
                Assert.Contains(extras, e => e.Contains("hlax64-runtime-file", StringComparison.OrdinalIgnoreCase));
                if (target == TargetTriple.LinuxX64SysV)
                    Assert.Contains(extras, e => e.Equals("-lc", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(cache))
                    Directory.Delete(cache, recursive: true);
            }
        }
    }

    [Fact]
    public void TryBuildLinkExtras_cp_includes_file_open_write_and_file_write()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "cp", "cp.hla64");
        var source = File.ReadAllText(sourcePath);

        foreach (var target in new[] { TargetTriple.WindowsX64MsAbi, TargetTriple.LinuxX64SysV })
        {
            var options = CompilationOptions.Default with { Target = target };
            var result = new Compilation(sourcePath, source, options).Process();
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));

            var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
            Assert.Contains("hlax_file_open_write", externs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hlax_file_write", externs, StringComparer.OrdinalIgnoreCase);

            var cache = Path.Combine(Path.GetTempPath(), "hlax64-cp-test-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                    result, isWindows: target == TargetTriple.WindowsX64MsAbi, cache, out var extras, out var error), error);
                Assert.Contains(extras, e => e.Contains("hlax64-runtime-file", StringComparison.OrdinalIgnoreCase));
                if (target == TargetTriple.LinuxX64SysV)
                    Assert.Contains(extras, e => e.Equals("-lc", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(cache))
                    Directory.Delete(cache, recursive: true);
            }
        }
    }

    [Fact]
    public void TryBuildLinkExtras_tee_includes_file_write_and_stdout_write()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "tee", "tee.hla64");
        var source = File.ReadAllText(sourcePath);

        foreach (var target in new[] { TargetTriple.WindowsX64MsAbi, TargetTriple.LinuxX64SysV })
        {
            var options = CompilationOptions.Default with { Target = target };
            var result = new Compilation(sourcePath, source, options).Process();
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));

            var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
            Assert.Contains("hlax_file_open_write", externs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hlax_file_write", externs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hlax_stdout_write", externs, StringComparer.OrdinalIgnoreCase);

            var cache = Path.Combine(Path.GetTempPath(), "hlax64-tee-test-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                    result, isWindows: target == TargetTriple.WindowsX64MsAbi, cache, out var extras, out var error), error);
                Assert.Contains(extras, e => e.Contains("hlax64-runtime-file", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(cache))
                    Directory.Delete(cache, recursive: true);
            }
        }
    }

    [Fact]
    public void TryBuildLinkExtras_strings_includes_file_mem_and_stdout_write()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "strings", "strings.hla64");
        var source = File.ReadAllText(sourcePath);

        foreach (var target in new[] { TargetTriple.WindowsX64MsAbi, TargetTriple.LinuxX64SysV })
        {
            var options = CompilationOptions.Default with { Target = target };
            var result = new Compilation(sourcePath, source, options).Process();
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));

            var externs = RuntimeObjectProvider.CollectRequiredExterns(result.LoweredFunctions).ToList();
            Assert.Contains("hlax_is_printable", externs, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("hlax_stdout_write", externs, StringComparer.OrdinalIgnoreCase);

            var cache = Path.Combine(Path.GetTempPath(), "hlax64-strings-test-" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                    result, isWindows: target == TargetTriple.WindowsX64MsAbi, cache, out var extras, out var error), error);
                Assert.Contains(extras, e => e.Contains("hlax64-runtime-file", StringComparison.OrdinalIgnoreCase));
                Assert.Contains(extras, e => e.Contains("hlax64-runtime-mem", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(cache))
                    Directory.Delete(cache, recursive: true);
            }
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
