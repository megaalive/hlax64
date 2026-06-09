using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public sealed class ControlFlowCompilerTests
{
    [Fact]
    public void Compile_IfNotEqual_EmitsJne()
    {
        var nasm = Emit("""
            program t;
            begin t;
                if(rax != 0) then
                    mov(1, rbx);
                endif;
            end t;
            """);
        Assert.Contains("jne", nasm, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compile_BreakOutsideLoop_ReportsHlax0074()
    {
        var compilation = new Compilation("test.hla64", """
            program t;
            begin t;
                break;
            end t;
            """, CompilationOptions.Default);
        var result = compilation.Process();
        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Contains("HLAX0074", StringComparison.Ordinal));
    }

    [Fact]
    public void Compile_BreakInsideWhile_EmitsBranchToEnd()
    {
        var nasm = Emit("""
            program t;
            begin t;
                mov(1, rcx);
                while(rcx > 0) do
                    break;
                endwhile;
            end t;
            """);
        Assert.Contains("jmp endwhile_", nasm, StringComparison.Ordinal);
    }

    private static string Emit(string source)
    {
        var compilation = new Compilation("(test)", source, CompilationOptions.Default);
        var result = compilation.Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var emitter = new NasmEmitter();
        return emitter.Emit(result.LoweredFunctions, result.StringLiterals);
    }
}
