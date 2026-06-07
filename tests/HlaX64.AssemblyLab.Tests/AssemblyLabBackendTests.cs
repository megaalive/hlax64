using HlaX64.AssemblyLab.Services;

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
    public void TargetChoices_includes_linux_and_windows()
    {
        Assert.Contains("linux-x64-sysv", AssemblyLabBackend.TargetChoices);
        Assert.Contains("windows-x64-msabi", AssemblyLabBackend.TargetChoices);
    }
}
