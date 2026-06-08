using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class ExamplesCurriculumTests
{
    [Fact]
    public void CurriculumManifest_LoadAll_HasExpectedPrograms()
    {
        var manifests = TestManifest.LoadAll("tests/examples-curriculum");
        Assert.Equal(58, manifests.Count);
        Assert.Contains(manifests, m => m.Name == "curriculum-hello");
        Assert.Contains(manifests, m => m.Name == "real-listfiles");
        Assert.Contains(manifests, m => m.Name == "real-filesize");
        Assert.Contains(manifests, m => m.Name == "real-exists");
        Assert.Contains(manifests, m => m.Name == "real-linecount");
        Assert.Contains(manifests, m => m.Name == "real-hexdump");
        Assert.Contains(manifests, m => m.Name == "real-wc");
        Assert.Contains(manifests, m => m.Name == "real-fnv1a");
        Assert.Contains(manifests, m => m.Name == "real-filemagic");
        Assert.Contains(manifests, m => m.Name == "real-cmp");
        Assert.Contains(manifests, m => m.Name == "interop-native-count-lines");
        Assert.Contains(manifests, m => m.Name == "interop-native-fnv1a");
        Assert.Contains(manifests, m => m.Name == "interop-native-sum-bytes");
        Assert.Contains(manifests, m => m.Name == "bugfarm-call-inside-loop");
        Assert.Contains(manifests, m => m.Name == "bugfarm-nested-while");
        Assert.Contains(manifests, m => m.Name == "bugfarm-register-pressure");
        Assert.Contains(manifests, m => m.Name == "bugfarm-deep-nested-if");
        Assert.Contains(manifests, m => m.Name == "bugfarm-many-locals");
        Assert.Contains(manifests, m => m.Name == "bugfarm-many-procedures");
        Assert.Contains(manifests, m => m.Name == "bugfarm-many-externs");
        Assert.Contains(manifests, m => m.Name == "bugfarm-large-static-buffer");
        Assert.Contains(manifests, m => m.Name == "bugfarm-many-stdout-args");
        Assert.Contains(manifests, m => m.Name == "linux-linecount");
        Assert.Contains(manifests, m => m.Name == "linux-exists");
        Assert.Contains(manifests, m => m.Name == "linux-wc");
        Assert.Contains(manifests, m => m.Name == "linux-fnv1a");
        Assert.Contains(manifests, m => m.Name == "curriculum-const-expr");
        Assert.Contains(manifests, m => m.Name == "curriculum-idiv-jmp");
        Assert.Contains(manifests, m => m.Name == "curriculum-dynamic-array");
        Assert.All(manifests, m => Assert.Contains("examples/", m.Source.Replace('\\', '/')));
    }
}
