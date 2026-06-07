using System.Text.Json;
using HlaX64.DebugAdapter;
using HlaX64.LanguageServer;

namespace HlaX64.Compiler.Tests;

public sealed class Round2BatchCTests
{
    [Fact]
    public void DapRequest_ParsesInitialize()
    {
        var json = """{"seq":1,"type":"request","command":"initialize","arguments":{}}""";
        var root = DapJson.Parse(json)!.Value;
        var req = DapRequest.TryParse(root);
        Assert.NotNull(req);
        Assert.Equal("initialize", req!.Command);
    }

    [Fact]
    public void DapHost_HandlesSetBreakpoints()
    {
        var input = new StringReader("""{"seq":2,"type":"request","command":"setBreakpoints","arguments":{"source":{"path":"main.hla64"},"breakpoints":[{"line":5}]}}""");
        var output = new StringWriter();
        var host = new DebugAdapterHost(input, output);
        host.Run();
        Assert.Contains("setBreakpoints", output.ToString());
        Assert.Contains("verified", output.ToString());
    }
}

public sealed class Round2BatchDTests
{
    [Fact]
    public void SourceMap_IncludesColumnAndEndFields()
    {
        const string source = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        var artifacts = HlaX64.Cli.Commands.CompilePipeline.Compile("(test)", source,
            HlaX64.Compiler.Options.CompilationOptions.Default with { EmitSourceMap = true });
        var entry = artifacts.SourceMap!.Entries.First(e => e.SourceColumn != null);
        Assert.NotNull(entry.EndLine);
    }

    [Fact]
    public void SignatureHelp_FallbackWhenParseErrors()
    {
        const string source = """
            program t;
            procedure Foo(a: int64, b: int64);
            begin Foo;
            // broken
            begin t;
                call Foo(
            end t;
            """;
        var help = LanguageServerEditorServices.GetSignatureHelp(source, 5, 13);
        Assert.NotNull(help);
    }

    [Fact]
    public void SemanticTokens_LegendIncludesModifiers()
    {
        var legend = LanguageServerEditorServices.GetSemanticTokensLegend();
        var json = JsonSerializer.Serialize(legend);
        Assert.Contains("declaration", json);
        Assert.Contains("readonly", json);
    }
}

public sealed class Round2BatchFTests
{
    [Fact]
    public void DifferentialManifest_LoadsFromTestsDifferential()
    {
        var root = FindRepoRoot();
        var manifestPath = Path.Combine(root, "tests", "differential", "simple", "manifest.json");
        Assert.True(File.Exists(manifestPath));
        var manifest = TestManifest.LoadFromJson(manifestPath);
        Assert.Equal(3, manifest.ExpectedExitCode);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "differential")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException();
    }
}
