using HlaX64.AssemblyLab.Services;

namespace HlaX64.AssemblyLab.Tests;

public class DebugBreakpointResolverTests
{
    [Fact]
    public void Resolve_without_source_map_uses__start_symbol()
    {
        var resolved = DebugBreakpointResolver.Resolve([5], "hello.hla64", "hello.nasm", null);
        Assert.Single(resolved);
        Assert.Equal("symbol", resolved[0].Kind);
        Assert.Equal("_start", resolved[0].Symbol);
    }
}
