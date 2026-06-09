using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public class CompilationTests
{
    [Fact]
    public void GetVersion_ReturnsExpectedVersion()
    {
        var version = Compilation.GetVersion();
        Assert.Equal("0.2.1-alpha", version);
    }
}