using HlaX64.Compiler;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class StringModelTests
{
    [Fact]
    public void Semantic_CstringVariable_AcceptsType()
    {
        const string source = """
            program p;
            procedure Main;
            var
                msg: cstring;
            begin Main;
                mov(&"hello", msg);
            end Main;
            begin p;
                call Main();
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Semantic_Utf8SliceFieldAccess_Compiles()
    {
        const string source = """
            program p;
            procedure Walk; @returns("rax");
            var
                slice: utf8slice;
            begin Walk;
                mov(&"abc", slice.ptr);
                mov(3, slice.len);
                mov(slice.len, rax);
            end Walk;
            begin p;
                call Walk();
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void Semantic_Utf8SliceProcedureParams_Compiles()
    {
        const string source = """
            program p;
            procedure UseSlice(data: ptr; len: uint64);
            begin UseSlice;
            end UseSlice;
            procedure Main;
            var
                s: utf8slice;
            begin Main;
                mov(&"hi", s.ptr);
                mov(2, s.len);
                call UseSlice(s.ptr, s.len);
            end Main;
            begin p;
                call Main();
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }
}
