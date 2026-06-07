using System.Text.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class ExamplesInvalidTests
{
    [Fact]
    public void InvalidExamples_catalog_covers_all_conformance_invalid_cases()
    {
        var root = FindExamplesInvalidRoot();
        var exampleCount = Directory.GetDirectories(root).Count(d =>
            File.Exists(Path.Combine(d, "manifest.json")) &&
            Directory.GetFiles(d, "*.hla64").Length > 0);
        Assert.Equal(21, exampleCount);
    }

    [Theory]
    [MemberData(nameof(GetInvalidExampleCases))]
    public void InvalidExample_reports_expected_diagnostic(string exampleDir)
    {
        var manifest = LoadManifest(exampleDir);
        var sourceName = Directory.GetFiles(exampleDir, "*.hla64").Single();
        var source = File.ReadAllText(sourceName);

        var warnings = manifest.EnableVerificationWarnings
            ? new CompilerWarnings(DefiniteAssignment: true, Unreachable: true, Liveness: true)
            : CompilationOptions.Default.Warnings;
        var options = CompilationOptions.Default with { Warnings = warnings };
        var result = new Compilation(exampleDir, source, options).Process();

        if (manifest.ExpectParseError)
        {
            Assert.False(result.Success);
            return;
        }

        if (manifest.ExpectWarningsOnly)
            Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        else
            Assert.False(result.Success);

        if (manifest.ExpectCodes is { Length: > 0 })
        {
            var codes = result.StructuredDiagnostics.Select(d => d.Code).ToHashSet();
            foreach (var expected in manifest.ExpectCodes)
                Assert.Contains(expected, codes);
        }

        var expectedCodesPath = Path.Combine(exampleDir, "expected-codes.txt");
        if (File.Exists(expectedCodesPath) && manifest.ExpectCodes is { Length: > 0 })
        {
            var listed = File.ReadAllLines(expectedCodesPath)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0);
            Assert.Equal(manifest.ExpectCodes, listed);
        }
    }

    public static IEnumerable<object[]> GetInvalidExampleCases()
    {
        var root = FindExamplesInvalidRoot();
        if (!Directory.Exists(root))
            yield break;

        foreach (var exampleDir in Directory.GetDirectories(root).OrderBy(d => d, StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(exampleDir, "manifest.json")))
                continue;
            if (!Directory.GetFiles(exampleDir, "*.hla64").Any())
                continue;
            yield return [exampleDir];
        }
    }

    private static string FindExamplesInvalidRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "examples", "99-invalid");
            if (Directory.Exists(candidate) &&
                Directory.GetFiles(candidate, "manifest.json", SearchOption.AllDirectories).Length > 0)
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        throw new DirectoryNotFoundException("Could not locate examples/99-invalid");
    }

    private static InvalidExampleManifest LoadManifest(string exampleDir)
    {
        var path = Path.Combine(exampleDir, "manifest.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<InvalidExampleManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException($"Bad manifest: {path}");
    }

    private sealed class InvalidExampleManifest
    {
        public string[]? ExpectCodes { get; set; }
        public bool ExpectWarningsOnly { get; set; }
        public bool EnableVerificationWarnings { get; set; }
        public bool ExpectParseError { get; set; }
    }
}
