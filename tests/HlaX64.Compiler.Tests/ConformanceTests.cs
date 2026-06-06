using System.Text.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class ConformanceTests
{
    private static string ConformanceRoot => FindConformanceRoot();

    private static string FindConformanceRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "conformance");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate tests/conformance");
    }

    [Theory]
    [MemberData(nameof(GetValidCases))]
    public void ValidSource_CompilesSuccessfully(string caseDir)
    {
        var manifest = LoadManifest(caseDir);
        var source = File.ReadAllText(Path.Combine(caseDir, manifest.Source));
        var result = new Compilation("(conformance)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));

        if (manifest.ExpectNasmContains is { Length: > 0 })
        {
            var emitter = new HlaX64.Backend.Nasm.Emitters.NasmEmitter();
            var nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals);
            foreach (var fragment in manifest.ExpectNasmContains)
                Assert.Contains(fragment, nasm, StringComparison.Ordinal);
        }
    }

    [Theory]
    [MemberData(nameof(GetInvalidCases))]
    public void InvalidSource_ReportsExpectedDiagnostic(string caseDir)
    {
        var manifest = LoadManifest(caseDir);
        var source = File.ReadAllText(Path.Combine(caseDir, manifest.Source));

        if (manifest.ExpectParseError)
        {
            var lexer = new Lexer(source);
            var parser = new Parser(lexer.Tokenize());
            Assert.Throws<ParseException>(() => parser.Parse());
            return;
        }

        var result = new Compilation("(conformance)", source).Process();
        Assert.False(result.Success);

        if (manifest.ExpectCodes is { Length: > 0 })
        {
            var codes = result.StructuredDiagnostics.Select(d => d.Code).ToHashSet();
            foreach (var expected in manifest.ExpectCodes)
                Assert.Contains(expected, codes);
        }
    }

    [Fact]
    public void ConformanceDirectory_ExistsWithCases()
    {
        Assert.True(Directory.Exists(ConformanceRoot));
        Assert.True(Directory.GetDirectories(Path.Combine(ConformanceRoot, "valid")).Length >= 1);
        Assert.True(Directory.GetDirectories(Path.Combine(ConformanceRoot, "invalid")).Length >= 3);
    }

    public static IEnumerable<object[]> GetValidCases()
    {
        var dir = Path.Combine(ConformanceRoot, "valid");
        if (!Directory.Exists(dir)) yield break;
        foreach (var caseDir in Directory.GetDirectories(dir))
            yield return [caseDir];
    }

    public static IEnumerable<object[]> GetInvalidCases()
    {
        var dir = Path.Combine(ConformanceRoot, "invalid");
        if (!Directory.Exists(dir)) yield break;
        foreach (var caseDir in Directory.GetDirectories(dir))
            yield return [caseDir];
    }

    private static ConformanceManifest LoadManifest(string caseDir)
    {
        var path = Path.Combine(caseDir, "manifest.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConformanceManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Bad manifest: {path}");
    }

    private sealed class ConformanceManifest
    {
        public string Source { get; set; } = "source.hla64";
        public bool ExpectParseError { get; set; }
        public string[]? ExpectCodes { get; set; }
        public string[]? ExpectNasmContains { get; set; }
    }
}
