using HlaX64.AssemblyLab.Services;
using HlaX64.AssemblyLab.ViewModels;
using HlaX64.Cli.Services;
using HlaX64.Cli.Toolchain;
using HlaX64.DebugAdapter;

namespace HlaX64.AssemblyLab.Tests;

public class AssemblyLabBackendTests
{
    private static string HelloPath =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "examples", "00-getting-started", "hello.hla64"));

    private const string MinimalSource = """
        program t;
        begin t;
            mov(1, rax);
            add(2, rax);
        end t;
        """;

    [Fact]
    public void Compile_hello_returns_diagnostics_and_nasm()
    {
        var source = File.ReadAllText(HelloPath);
        var backend = new AssemblyLabBackend();
        var result = backend.Compile(HelloPath, source);

        Assert.True(result.Success);
        Assert.NotNull(result.NasmText);
        Assert.Contains("section", result.NasmText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(result.IrText);
        Assert.NotNull(result.AbiText);
    }

    [Fact]
    public void Compile_invalid_source_reports_structured_diagnostics()
    {
        var backend = new AssemblyLabBackend();
        const string bad = """
            program bad;
            begin bad;
                movz(1, rax);
            end bad;
            """;
        var result = backend.Compile("(bad)", bad);

        Assert.False(result.Success);
        Assert.NotEmpty(result.Diagnostics);
    }

    [Fact]
    public void Build_with_source_map_produces_hlamap()
    {
        var backend = new AssemblyLabBackend();
        var outDir = Path.Combine(Path.GetTempPath(), "hlax64-lab-test-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var build = backend.Build("(hello)", MinimalSource, "linux-x64-sysv", outDir);
            if (!build.Success)
            {
                Assert.NotNull(build.NasmFile);
                Assert.True(File.Exists(build.NasmFile));
                return;
            }

            Assert.NotNull(build.SourceMapFile);
            Assert.True(File.Exists(build.SourceMapFile));
            Assert.NotNull(build.SourceMap);
            Assert.NotEmpty(build.SourceMap!.Entries);

            var map = backend.LoadSourceMap(build.SourceMapFile!);
            Assert.NotNull(map);
            Assert.NotEmpty(map!.Entries);
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void Source_map_lookup_returns_entry_for_inline_source()
    {
        var backend = new AssemblyLabBackend();
        var result = backend.Compile("(stack-test)", MinimalSource);

        Assert.True(result.Success);
        Assert.NotNull(result.SourceMap);
        Assert.NotEmpty(result.SourceMap!.Entries);

        var entry = result.SourceMap.Entries[0];
        Assert.True(entry.IrId > 0);
        if (entry.NasmLine != null)
        {
            var nasmLine = backend.FindNasmLineForSource(result.SourceMap, entry.SourceLine);
            Assert.Equal(entry.NasmLine, nasmLine);
        }
    }

    [Fact]
    public void AnalyzeCapabilities_detects_stdout_put()
    {
        var source = File.ReadAllText(HelloPath);
        var backend = new AssemblyLabBackend();
        var cap = backend.AnalyzeCapabilities(source);

        Assert.True(cap.HasStdoutPut);
        var summary = backend.SummarizeCapabilities(cap);
        Assert.Contains("hasStdoutPut: True", summary);
    }

    [Fact]
    public void ExportProofBundle_without_linker_succeeds_compile_only()
    {
        var backend = new AssemblyLabBackend();
        var outDir = Path.Combine(Path.GetTempPath(), "hlax64-lab-proof-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = backend.ExportProofBundle("(proof)", MinimalSource, "windows-x64-msabi", outDir);
            if (LinkerTool.TryFindWindowsLinker(out _, out _, out _))
            {
                Assert.True(result.Success);
                Assert.NotNull(result.ProofBundleDir);
                Assert.True(File.Exists(Path.Combine(result.ProofBundleDir!, "build.json")));
                return;
            }

            Assert.True(result.Success, result.Message);
            Assert.NotNull(result.ProofBundleDir);
            Assert.Contains("compile-only", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.True(File.Exists(Path.Combine(result.ProofBundleDir!, "capabilities.json")));
            var buildJson = File.ReadAllText(Path.Combine(result.ProofBundleDir!, "build.json"));
            Assert.Contains("\"compileOnly\": true", buildJson);
        }
        finally
        {
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, recursive: true);
        }
    }

    [Fact]
    public void ResolveDefaultTarget_returns_known_target()
    {
        var target = AssemblyLabBackend.ResolveDefaultTarget();
        Assert.Contains(target, AssemblyLabBackend.TargetChoices);
    }

    [Fact]
    public void ResolveToolExecutable_finds_lld_link_in_common_llvm_path()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var resolved = LinkerTool.ResolveToolExecutable("lld-link");
        if (File.Exists(@"C:\Program Files\LLVM\bin\lld-link.exe"))
        {
            Assert.NotNull(resolved);
            Assert.True(File.Exists(resolved!));
        }
    }

    [Fact]
    public void TryFindWindowsLinker_uses_resolved_llvm_path()
    {
        if (!OperatingSystem.IsWindows())
            return;

        if (!LinkerTool.TryFindWindowsLinker(out var path, out _, out _))
            return;

        Assert.True(File.Exists(path) || LinkerTool.ResolveToolExecutable("lld-link") != null);
    }

    [Fact]
    public void GetDisasmText_includes_source_map_columns()
    {
        var backend = new AssemblyLabBackend();
        const string source = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        var compile = backend.Compile("(t)", source);
        Assert.True(compile.Success);
        var disasm = backend.GetDisasmText(compile.NasmText, compile.SourceMap);
        Assert.Contains("src:", disasm);
    }

    [Fact]
    public void LabToolchainService_detect_returns_summary()
    {
        var info = LabToolchainService.Detect();
        var summary = LabToolchainService.Summarize(info);
        Assert.Contains("WSL:", summary);
    }

    [Fact]
    public void ToggleBreakpoint_adds_and_removes_line()
    {
        var vm = new MainWindowViewModel();
        vm.ToggleBreakpoint(3);
        Assert.Contains(3, vm.BreakpointLines);
        vm.ToggleBreakpoint(3);
        Assert.DoesNotContain(3, vm.BreakpointLines);
    }

    [Fact]
    public void Syntax_grammar_files_exist_in_output()
    {
        var baseDir = AppContext.BaseDirectory;
        Assert.True(File.Exists(Path.Combine(baseDir, "Assets", "syntax", "hla64", "package.json")));
        Assert.True(File.Exists(Path.Combine(baseDir, "Assets", "syntax", "hla64", "syntaxes", "hla64.tmLanguage.json")));
    }

    [Fact]
    public void TargetChoices_includes_linux_and_windows()
    {
        Assert.Contains("linux-x64-sysv", AssemblyLabBackend.TargetChoices);
        Assert.Contains("windows-x64-msabi", AssemblyLabBackend.TargetChoices);
    }

    [Fact]
    public void ExplainForAgent_returns_json_with_suggested_fix()
    {
        const string bad = """
            program bad;
            begin bad;
                movz(1, rax);
            end bad;
            """;
        var backend = new AssemblyLabBackend();
        var json = backend.ExplainForAgent("(bad)", bad, "linux-x64-sysv");
        Assert.Contains("suggestedFix", json, StringComparison.Ordinal);
        Assert.Contains("diagnostics", json, StringComparison.Ordinal);
        Assert.Contains("movz", json, StringComparison.Ordinal);
    }

    [Fact]
    public void GetPlanText_includes_emit_and_link_steps()
    {
        var backend = new AssemblyLabBackend();
        var plan = backend.GetPlanText(HelloPath, "linux-x64-sysv");
        Assert.Contains("emit-nasm", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("assemble", plan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("link", plan, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDiffText_detects_procedure_change()
    {
        const string oldSrc = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        const string newSrc = """
            program t;
            begin t;
                mov(2, rax);
                add(1, rax);
            end t;
            """;
        var backend = new AssemblyLabBackend();
        var diff = backend.GetDiffText(oldSrc, newSrc);
        Assert.Contains("changedProcedures", diff, StringComparison.Ordinal);
    }

    [Fact]
    public void SemanticDiffService_reports_added_extern()
    {
        const string oldSrc = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        const string newSrc = """
            program t;
            extern procedure puts(msg: cstring): int32 from "libc.so";
            begin t;
                mov(1, rax);
            end t;
            """;
        var diff = SemanticDiffService.Compare(oldSrc, newSrc);
        var json = System.Text.Json.JsonSerializer.Serialize(diff);
        Assert.Contains("addedExterns", json, StringComparison.Ordinal);
        Assert.Contains("puts", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExplainAgentService_SuggestFix_returns_template_for_unknown_mnemonic()
    {
        var fix = ExplainAgentService.SuggestFix(new HlaX64.Compiler.Diagnostics.Diagnostic(
            "HLAX0003", HlaX64.Compiler.Diagnostics.DiagnosticSeverity.Error, "unknown mnemonic", 1, 1));
        Assert.NotNull(fix);
        var json = System.Text.Json.JsonSerializer.Serialize(fix);
        Assert.Contains("list-instructions", json, StringComparison.Ordinal);
    }

    [Fact]
    public void DebugBackendFactory_selects_platform_backend()
    {
        var backend = DebugBackendFactory.CreateDefault();
        if (OperatingSystem.IsLinux())
            Assert.IsType<GdbBackend>(backend);
        else if (OperatingSystem.IsWindows())
            Assert.IsType<LldbBackend>(backend);
        backend.Dispose();
    }

    [Fact]
    public void PlanApproved_gates_build_command()
    {
        var vm = new MainWindowViewModel();
        Assert.False(vm.BuildCommand.CanExecute(null));
        vm.PlanApproved = true;
        Assert.True(vm.BuildCommand.CanExecute(null));
    }
}
