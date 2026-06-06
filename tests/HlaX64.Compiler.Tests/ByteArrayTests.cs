using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class ByteArrayTests
{
    [Fact]
    public void Analyze_ByteArrayDeclaration_NoError()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: byte[4];
            begin P;
                mov(10, buf[0]);
                mov(buf[3], rax);
            end P;
            begin t;
            end t;
            """;
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        var varNode = Assert.IsType<VariableNode>(proc.Variables[0]);
        Assert.Equal(4, varNode.ElementCount);
        Assert.Equal("byte", varNode.Type);

        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.False(diags.HasErrors);
    }

    [Fact]
    public void Emit_ByteArrayLiteralIndex_UsesByteStride()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: byte[4];
            begin P;
                mov(20, buf[1]);
                mov(buf[1], rax);
            end P;
            begin t;
                mov(0, rax);
            end t;
            """;
        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var emitter = new HlaX64.Backend.Nasm.Emitters.NasmEmitter();
        var nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("byte [rbp-", nasm);
        Assert.Contains("+1]", nasm);
        Assert.DoesNotContain("+8]", nasm);
    }
}
