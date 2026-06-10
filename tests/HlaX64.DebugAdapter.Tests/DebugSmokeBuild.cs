using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

internal static class DebugSmokeBuild
{
    private const string MinimalSource = """
        program dbgstep;
        begin dbgstep;
            mov(1, rax);
            mov(2, rbx);
            mov(3, rcx);
            mov(4, rdx);
            mov(5, rsi);
            mov(6, rdi);
            mov(7, r8);
            mov(8, r9);
            mov(9, r10);
            mov(10, r11);
            mov(11, r12);
            mov(12, r13);
        end dbgstep;
        """;

    public static bool TryBuild(out string? executablePath, out string? skipReason)
    {
        executablePath = null;
        skipReason = null;

        if (!NasmTool.TryFindNasm(out var nasmPath))
        {
            skipReason = "NASM not found";
            return false;
        }

        var isWindows = OperatingSystem.IsWindows()
                        && LinkerTool.TryFindWindowsLinker(out _, out _, out _);
        if (OperatingSystem.IsWindows() && !isWindows)
        {
            skipReason = "Windows linker not found";
            return false;
        }

        if (!isWindows && !LinkerTool.TryFindLinker(out _, out _, out _))
        {
            skipReason = "ELF linker not found";
            return false;
        }

        if (!DebugBackendFactory.CreateDefault().IsAvailable)
        {
            skipReason = DebugBackendFactory.GetUnavailableReason() ?? "debugger not found";
            return false;
        }

        var outputDir = Path.Combine(Path.GetTempPath(), "hlax64-debug-smoke-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(outputDir);

        var sourcePath = Path.Combine(outputDir, "dbgstep.hla64");
        var target = isWindows ? "windows-x64-msabi" : "linux-x64-sysv";
        var options = CompilationOptions.Default with
        {
            Target = TargetTriple.Parse(target),
            EmitSourceMap = true
        };

        var nasmFile = Path.Combine(outputDir, "dbgstep.nasm");
        var objExt = isWindows ? ".obj" : ".o";
        var objFile = Path.Combine(outputDir, "dbgstep" + objExt);
        var outputFile = Path.Combine(outputDir, isWindows ? "dbgstep.exe" : "dbgstep");

        try
        {
            File.WriteAllText(sourcePath, MinimalSource);
            var artifacts = CompilePipeline.Compile(sourcePath, MinimalSource, options);
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: isWindows ? "win64" : "elf64", emitDebugInfo: true))
            {
                skipReason = nasmError;
                return false;
            }

            var linkExtras = artifacts.Result.LinkLibraries.ToList();
            if (isWindows)
            {
                if (!RuntimeObjectProvider.TryBuildLinkExtras(
                        artifacts.Result, true, outputDir, out linkExtras, out var runtimeError))
                {
                    skipReason = runtimeError ?? "runtime link setup failed";
                    return false;
                }

                DebugProcessCleanup.TryEnsureWritable(outputFile);
                if (!LinkerTool.TryLinkWindows(objFile, outputFile, out var linkError,
                        extraLibraries: linkExtras, emitDebugInfo: true))
                {
                    skipReason = linkError;
                    return false;
                }
            }
            else if (!LinkerTool.TryLink(objFile, outputFile, out var linkError, out _, extraLibraries: linkExtras))
            {
                skipReason = linkError;
                return false;
            }

            if (!isWindows)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{outputFile}\"",
                        UseShellExecute = false
                    })?.WaitForExit(2000);
                }
                catch
                {
                    // optional on non-Unix hosts
                }
            }

            executablePath = outputFile;
            return File.Exists(outputFile);
        }
        catch (Exception ex)
        {
            skipReason = ex.Message;
            return false;
        }
    }
}
