using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class ExamplesCurriculumTests
{
    [Fact]
    public void CurriculumManifest_LoadAll_HasExpectedPrograms()
    {
        var manifests = TestManifest.LoadAll("tests/examples-curriculum");
        Assert.Equal(110, manifests.Count);
        Assert.Contains(manifests, m => m.Name == "curriculum-hello");
        Assert.Contains(manifests, m => m.Name == "real-listfiles");
        Assert.Contains(manifests, m => m.Name == "real-filesize");
        Assert.Contains(manifests, m => m.Name == "real-exists");
        Assert.Contains(manifests, m => m.Name == "real-linecount");
        Assert.Contains(manifests, m => m.Name == "real-hexdump");
        Assert.Contains(manifests, m => m.Name == "real-wc");
        Assert.Contains(manifests, m => m.Name == "real-fnv1a");
        Assert.Contains(manifests, m => m.Name == "real-cat");
        Assert.Contains(manifests, m => m.Name == "real-strings");
        Assert.Contains(manifests, m => m.Name == "real-cp");
        Assert.Contains(manifests, m => m.Name == "real-tee");
        Assert.Contains(manifests, m => m.Name == "real-head");
        Assert.Contains(manifests, m => m.Name == "real-nl");
        Assert.Contains(manifests, m => m.Name == "real-grep");
        Assert.Contains(manifests, m => m.Name == "real-filemagic");
        Assert.Contains(manifests, m => m.Name == "real-cmp");
        Assert.Contains(manifests, m => m.Name == "real-pid");
        Assert.Contains(manifests, m => m.Name == "real-hostname");
        Assert.Contains(manifests, m => m.Name == "real-uptime");
        Assert.Contains(manifests, m => m.Name == "real-meminfo");
        Assert.Contains(manifests, m => m.Name == "real-machine");
        Assert.Contains(manifests, m => m.Name == "real-netcheck");
        Assert.Contains(manifests, m => m.Name == "real-tcpget");
        Assert.Contains(manifests, m => m.Name == "real-httpget");
        Assert.Contains(manifests, m => m.Name == "real-curl");
        Assert.Contains(manifests, m => m.Name == "real-dnslookup");
        Assert.Contains(manifests, m => m.Name == "real-cpucount");
        Assert.Contains(manifests, m => m.Name == "real-diskfree");
        Assert.Contains(manifests, m => m.Name == "real-procmem");
        Assert.Contains(manifests, m => m.Name == "real-loadavg");
        Assert.Contains(manifests, m => m.Name == "real-machine2");
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
        Assert.Contains(manifests, m => m.Name == "linux-cat");
        Assert.Contains(manifests, m => m.Name == "linux-strings");
        Assert.Contains(manifests, m => m.Name == "linux-cp");
        Assert.Contains(manifests, m => m.Name == "linux-tee");
        Assert.Contains(manifests, m => m.Name == "linux-head");
        Assert.Contains(manifests, m => m.Name == "linux-nl");
        Assert.Contains(manifests, m => m.Name == "linux-grep");
        Assert.Contains(manifests, m => m.Name == "linux-cmp");
        Assert.Contains(manifests, m => m.Name == "linux-hexdump");
        Assert.Contains(manifests, m => m.Name == "linux-filemagic");
        Assert.Contains(manifests, m => m.Name == "linux-pid");
        Assert.Contains(manifests, m => m.Name == "linux-hostname");
        Assert.Contains(manifests, m => m.Name == "linux-uptime");
        Assert.Contains(manifests, m => m.Name == "linux-meminfo");
        Assert.Contains(manifests, m => m.Name == "linux-filesize");
        Assert.Contains(manifests, m => m.Name == "linux-machine");
        Assert.Contains(manifests, m => m.Name == "linux-netcheck");
        Assert.Contains(manifests, m => m.Name == "linux-tcpget");
        Assert.Contains(manifests, m => m.Name == "linux-httpget");
        Assert.Contains(manifests, m => m.Name == "linux-curl");
        Assert.Contains(manifests, m => m.Name == "linux-dnslookup");
        Assert.Contains(manifests, m => m.Name == "linux-cpucount");
        Assert.Contains(manifests, m => m.Name == "linux-diskfree");
        Assert.Contains(manifests, m => m.Name == "linux-procmem");
        Assert.Contains(manifests, m => m.Name == "linux-loadavg");
        Assert.Contains(manifests, m => m.Name == "linux-machine2");
        Assert.Contains(manifests, m => m.Name == "curriculum-const-expr");
        Assert.Contains(manifests, m => m.Name == "curriculum-idiv-jmp");
        Assert.Contains(manifests, m => m.Name == "curriculum-dynamic-array");
        Assert.Contains(manifests, m => m.Name == "curriculum-dynamic-array-heap");
        Assert.Contains(manifests, m => m.Name == "curriculum-euler001-bruteforce");
        Assert.Contains(manifests, m => m.Name == "curriculum-euler004-palindrome");
        Assert.Contains(manifests, m => m.Name == "curriculum-euler010-primes");
        Assert.All(manifests, m => Assert.Contains("examples/", m.Source.Replace('\\', '/')));
    }
}
