using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class ExamplesCurriculumTests
{
    [Fact]
    public void CurriculumManifest_LoadAll_HasEighteenPrograms()
    {
        var manifests = TestManifest.LoadAll("tests/examples-curriculum");
        Assert.Equal(18, manifests.Count);
        Assert.Contains(manifests, m => m.Name == "curriculum-hello");
        Assert.All(manifests, m => Assert.Contains("examples/", m.Source.Replace('\\', '/')));
    }
}
