using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Verification;

namespace HlaX64.Compiler.Tests;

public sealed class StackVerifierTests
{
    [Fact]
    public void Verify_ValidProcedure_HasPrologueAndEpilogue()
    {
        var source = """
            program t;
            procedure Add; @returns("rax");
            var x: int64;
            begin Add;
                mov(1, x);
                mov(x, rax);
            end Add;
            begin t;
                mov(0, rax);
            end t;
            """;
        var compile = new Compilation("(test)", source).Process();
        Assert.True(compile.Success);

        var result = StackVerifier.Verify(compile.IrFunctions, compile.LoweredFunctions);
        Assert.True(result.Success, string.Join("; ", result.Issues.Select(i => i.Message)));
        Assert.Contains(result.Procedures, p => p.Procedure == "Add" && p.HasPrologue && p.HasEpilogue);
    }

    [Fact]
    public void Verify_StackFrameSize_IsAligned()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var a: int64;
            var b: int64;
            begin P;
                mov(0, rax);
            end P;
            begin t;
                mov(0, rax);
            end t;
            """;
        var compile = new Compilation("(test)", source).Process();
        var result = StackVerifier.Verify(compile.IrFunctions, compile.LoweredFunctions);
        Assert.All(result.Procedures, p => Assert.True(p.AlignmentOk));
    }
}
