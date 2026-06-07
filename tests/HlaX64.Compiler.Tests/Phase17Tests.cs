using HlaX64.Compiler;

namespace HlaX64.Compiler.Tests;

public sealed class Phase17Tests
{
    [Fact]
    public void ExternProcedure_ParsesAndCompiles()
    {
        const string source = """
            program t;
            extern procedure puts(msg: cstring): int32 from "libc.so";
            begin t;
                call puts(&"hi");
                mov(rax, rbx);
            end t;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.Contains("-lc", result.LinkLibraries);
    }

    [Fact]
    public void TypeAlias_FunctionPointer_IndirectCall()
    {
        const string source = """
            program t;
            type Fn := procedure(): int64;
            procedure Go(fn: Fn); @returns("rax");
            begin Go;
                call fn();
            end Go;
            begin t;
                mov(0, rax);
            end t;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter()
            .Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);
        Assert.Contains("call rax", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Float64Parameter_EmitsMovsdInPrologue()
    {
        const string source = """
            program t;
            procedure F(v: float64); @returns("xmm0");
            begin F;
                movsd(v, xmm0);
            end F;
            begin t;
                mov(0, rax);
            end t;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter()
            .Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);
        Assert.Contains("movsd", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void VariadicExtern_ParsesAndRegisters()
    {
        const string source = """
            program t;
            extern variadic procedure printf(fmt: cstring): int32;
            begin t;
            end t;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        Assert.True(result.ExternProcedures.TryGet("printf", out var ext));
        Assert.True(ext.IsVariadic);
    }

    [Fact]
    public void VariadicPrintf_LowersSysVRegisters()
    {
        const string source = """
            program t;
            extern variadic procedure printf(fmt: cstring): int32 from "libc.so";
            begin t;
                call printf(&"sum=%d\n", 42);
            end t;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter()
            .Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);
        Assert.Contains("call printf", nasm, StringComparison.Ordinal);
        Assert.Contains("mov al, 0", nasm, StringComparison.Ordinal);
        Assert.Contains("mov rdi", nasm, StringComparison.Ordinal);
        Assert.Contains("mov rsi", nasm, StringComparison.Ordinal);
    }
}
