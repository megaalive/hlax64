using HlaX64.Compiler;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class GlobalDataTests
{
    [Fact]
    public void Parse_StaticBlock_ParsesDeclarations()
    {
        const string source = """
            program p;
            static
                counter: uint64 := 0;
                buffer: byte[256];
            endstatic;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        Assert.Single(program.Statics);
        var block = Assert.IsType<Ast.StaticBlockNode>(program.Statics[0]);
        Assert.Equal(2, block.Declarations.Count);
        Assert.NotNull(block.Declarations[0].Initializer);
        Assert.Null(block.Declarations[1].Initializer);
    }

    [Fact]
    public void Semantic_StaticSymbol_RegistersGlobal()
    {
        const string source = """
            program p;
            static
                counter: uint64 := 42;
            endstatic;
            begin p;
                mov(counter, rax);
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var analyzer = new SemanticAnalyzer();
        var diags = analyzer.Analyze(program);
        Assert.False(diags.HasErrors);
        Assert.True(analyzer.GlobalData.Contains("counter"));
    }

    [Fact]
    public void Compile_StaticCounter_EmitsDataSection()
    {
        const string source = """
            program p;
            static
                counter: uint64 := 1;
                buffer: byte[4];
            endstatic;
            begin p;
                mov(1, counter);
                mov(counter, rax);
                mov(&counter, rax);
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter()
            .Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);
        Assert.Contains("section .bss", nasm);
        Assert.Contains("buffer resb 4", nasm, StringComparison.Ordinal);
        Assert.Contains("counter dq 1", nasm, StringComparison.Ordinal);
        Assert.Contains("mov [counter]", nasm, StringComparison.Ordinal);
        Assert.Contains("lea rax, [counter]", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_CompareStaticGlobal_EmitsMemoryOperandInCmp()
    {
        const string source = """
            program p;
            static
                len: uint64 := 0;
            endstatic;
            begin p;
                mov(0, rcx);
                if(rcx < len) then
                    mov(1, rax);
                else
                    mov(0, rax);
                endif;
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter()
            .Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);
        Assert.Contains("cmp rcx, [len]", nasm, StringComparison.Ordinal);
        Assert.DoesNotContain("global:len", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_DuplicateStatic_ReportsHlax0045()
    {
        const string source = """
            program p;
            static
                x: int64 := 1;
                x: int64 := 2;
            endstatic;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0045");
    }
}
