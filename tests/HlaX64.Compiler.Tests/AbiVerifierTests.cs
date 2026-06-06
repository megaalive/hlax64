using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Verification;

namespace HlaX64.Compiler.Tests;

public sealed class AbiVerifierTests
{
    [Fact]
    public void Verify_SysVProcedure_MapsFirstParamToRdi()
    {
        var source = """
            program t;
            procedure Add(a:int64; b:int64); @returns("rax");
            begin Add;
                mov(a, rax);
                add(b, rax);
            end Add;
            begin t;
                mov(0, rax);
            end t;
            """;
        var options = CompilationOptions.Default;
        var compile = new Compilation("(test)", source, options).Process();
        Assert.True(compile.Success);

        var result = AbiVerifier.Verify(
            options, compile.IrFunctions, compile.ExternProcedures, compile.RecordTypes, compile.ProcedureTypes);

        var add = Assert.Single(result.Procedures, p => p.Name == "Add");
        Assert.Equal("rdi", add.Parameters[0].Register);
        Assert.Equal("rsi", add.Parameters[1].Register);
        Assert.Equal("rax", add.ReturnRegister);
    }

    [Fact]
    public void Verify_ExternProcedure_ListedInReport()
    {
        var source = """
            program t;
            extern procedure puts(s: ptr): int32 from "libc";
            procedure Main; @returns("rax");
            begin Main;
                mov(0, rax);
            end Main;
            begin t;
                mov(0, rax);
            end t;
            """;
        var compile = new Compilation("(test)", source).Process();
        Assert.True(compile.Success);

        var result = AbiVerifier.Verify(
            CompilationOptions.Default, compile.IrFunctions, compile.ExternProcedures,
            compile.RecordTypes, compile.ProcedureTypes);

        Assert.Contains("puts", result.ExternSymbols);
    }
}
