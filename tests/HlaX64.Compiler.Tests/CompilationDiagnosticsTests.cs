using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class CompilationDiagnosticsTests
{
    [Fact]
    public void Process_ParseError_populates_structured_diagnostics()
    {
        var result = new Compilation("(test)", """
            program hello;
            begin hello;
                mov(1, rax
            end hello;
            """).Process();

        Assert.False(result.Success);
        var diag = Assert.Single(result.StructuredDiagnostics);
        Assert.Equal("HLAX1000", diag.Code);
        Assert.True(diag.Line > 0);
    }

    [Fact]
    public void Process_SemanticErrors_populate_structured_diagnostics()
    {
        var result = new Compilation("(test)", """
            program hello;
            begin foo;
                mov(1, raxz);
            end hello;
            """).Process();

        Assert.False(result.Success);
        var codes = result.StructuredDiagnostics.Select(d => d.Code).ToHashSet();
        Assert.Contains("HLAX0010", codes);
        Assert.Contains("HLAX0011", codes);
        Assert.Contains("HLAX0012", codes);
    }
}
