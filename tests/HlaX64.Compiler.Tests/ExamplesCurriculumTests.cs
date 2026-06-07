using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class ExamplesCurriculumTests
{
    [Fact]
    public void CurriculumManifest_LoadAll_HasExpectedPrograms()
    {
        var manifests = TestManifest.LoadAll("tests/examples-curriculum");
        Assert.Equal(33, manifests.Count);
        Assert.Contains(manifests, m => m.Name == "curriculum-hello");
        Assert.Contains(manifests, m => m.Name == "real-listfiles");
        Assert.Contains(manifests, m => m.Name == "real-filesize");
        Assert.Contains(manifests, m => m.Name == "real-exists");
        Assert.Contains(manifests, m => m.Name == "real-linecount");
        Assert.All(manifests, m => Assert.Contains("examples/", m.Source.Replace('\\', '/')));
    }
}
