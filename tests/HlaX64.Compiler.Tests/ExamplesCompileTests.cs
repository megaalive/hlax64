using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class ExamplesCompileTests
{
    public static IEnumerable<object[]> ExampleFiles => FindExampleFiles().Select(f => new object[] { f });

    [Theory]
    [MemberData(nameof(ExampleFiles))]
    public void Example_EmitsNasmWithoutErrors(string path)
    {
        var source = File.ReadAllText(path);
        var result = new Compilation(path, source).Process();
        Assert.True(result.Success, $"{path}: {string.Join("; ", result.Diagnostics)}");
        Assert.NotEmpty(result.LoweredFunctions);
    }

    [Fact]
    public void ExamplesDirectory_HasAtLeastTwentyPrograms()
    {
        Assert.True(FindExampleFiles().Count >= 20);
    }

    private static List<string> FindExampleFiles()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var examples = Path.Combine(dir.FullName, "examples");
            if (Directory.Exists(examples))
            {
                return Directory.GetFiles(examples, "*.hla64", SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate examples/");
    }
}
