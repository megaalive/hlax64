using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class LinuxArgvTests
{
    [Fact]
    public void SysV_linecount_emits_argv_bootstrap_at_start()
    {
        var repoRoot = FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "linecount", "linecount.hla64");
        var source = File.ReadAllText(sourcePath);
        var options = CompilationOptions.Default with { Target = TargetTriple.LinuxX64SysV };
        var artifacts = CompilePipeline.Compile(sourcePath, source, options);

        Assert.Contains("call hlax_argv_save_from_stack", artifacts.NasmCode, StringComparison.Ordinal);
        Assert.Contains("mov rdi, rsp", artifacts.NasmCode, StringComparison.Ordinal);

        var externs = RuntimeObjectProvider.CollectRequiredExterns(artifacts.Result.LoweredFunctions).ToList();
        Assert.Contains("hlax_argv_save_from_stack", externs, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("hlax_argv_init", externs, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Linux_argv_exit_count_runs_under_wsl_when_available()
    {
        if (!LinkerTool.TryFindLinker(out var linker, out _, out _))
            return;
        if (!linker.Equals("wsl", StringComparison.OrdinalIgnoreCase))
            return;
        if (!NasmTool.TryFindNasm(out _))
            return;

        const string source = """
            program argv_exit_count;
            extern procedure hlax_argv_count(): int64;
            begin argv_exit_count;
                call hlax_argv_count();
                mov(rax, rbx);
            end argv_exit_count;
            """;

        var options = CompilationOptions.Default with { Target = TargetTriple.LinuxX64SysV };
        var cache = Path.Combine(Path.GetTempPath(), "hlax64-linux-argv-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var artifacts = CompilePipeline.Compile("(argv_exit_count)", source, options);
            Directory.CreateDirectory(cache);
            var nasmFile = Path.Combine(cache, "argv_exit_count.nasm");
            var objFile = Path.Combine(cache, "argv_exit_count.o");
            var exeFile = Path.Combine(cache, "argv_exit_count");
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            var nasmTool = new NasmTool(NasmTool.TryFindNasm(out var nasmPath) ? nasmPath : null);
            Assert.True(nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: "elf64"), nasmError);
            Assert.True(RuntimeObjectProvider.TryBuildLinkExtras(
                artifacts.Result, isWindows: false, cache, out var extras, out var runtimeError), runtimeError);
            Assert.True(LinkerTool.TryLink(objFile, exeFile, out var linkError, out _, extraLibraries: extras), linkError);

            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = $"{LinkerTool.ToWslPath(exeFile)} one two",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            Assert.NotNull(process);
            Assert.True(process!.WaitForExit(15000), "argv_exit_count did not exit within 15s");
            Assert.Equal(3, process.ExitCode);
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
