using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class ExpressionAssignTests
{
    [Fact]
    public void Parse_RuntimeAssign_ParsesExpression()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                x := (1 + 2) * 3;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        var assign = Assert.IsType<AssignExprNode>(proc.Body[0]);
        Assert.IsType<IdentifierNode>(assign.Target);
        var expr = Assert.IsType<BinaryExprNode>(assign.Expression);
        Assert.Equal("*", expr.Operator);
    }

    [Fact]
    public void Parse_RegisterAssign_ParsesComparisonExpression()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var
                a: int64;
                b: int64;
            begin P;
                mov(1, a);
                mov(2, b);
                rax := a == b;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        var assign = Assert.IsType<AssignExprNode>(proc.Body[2]);
        Assert.IsType<RegisterNode>(assign.Target);
        var expr = Assert.IsType<BinaryExprNode>(assign.Expression);
        Assert.Equal("==", expr.Operator);
    }

    [Fact]
    public void Lexer_EmitsComparisonTokens()
    {
        var tokens = new Lexer("a == b != c <= d >= e").Tokenize();
        Assert.Contains(tokens, t => t.Type == TokenType.DoubleEquals);
        Assert.Contains(tokens, t => t.Type == TokenType.NotEquals);
        Assert.Contains(tokens, t => t.Type == TokenType.LessOrEqual);
        Assert.Contains(tokens, t => t.Type == TokenType.GreaterOrEqual);
    }

    [Fact]
    public void Semantic_ValidAssign_NoErrors()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            const
                Mask := $FF;
            endconst;
            var value: int64;
            begin P;
                mov(100, value);
                value := (value + 1) & Mask;
                rax := value >> 2;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.False(diags.HasErrors);
    }

    [Fact]
    public void Semantic_InvalidTarget_ReportsHlax0035()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var buf: byte[4];
            begin P;
                buf := 1;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0035");
    }

    [Fact]
    public void Semantic_UnknownNameInExpr_ReportsHlax0036()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                x := missing + 1;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0036");
    }

    [Fact]
    public void Semantic_ArrayInExpr_ReportsHlax0037()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var
                data: int64[4];
                x: int64;
            begin P;
                x := data + 1;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0037");
    }

    [Fact]
    public void Semantic_RuntimeDivideByZero_ReportsHlax0038()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                x := 10 / 0;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0038");
    }

    [Fact]
    public void Compile_ExpressionAssign_LowersArithmeticAndBitwise()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var
                a: int64;
                b: int64;
            begin P;
                mov(10, a);
                mov(3, b);
                rax := (a + b) * 2;
                rax := rax & $FF;
            end P;
            begin p;
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter().Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("add rax", nasm, StringComparison.Ordinal);
        Assert.Contains("imul rax", nasm, StringComparison.Ordinal);
        Assert.Contains("and rax", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Compile_ComparisonAssign_LowersSetcc()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            var
                a: int64;
                b: int64;
            begin P;
                mov(1, a);
                mov(2, b);
                rax := a < b;
            end P;
            begin p;
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter().Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("setl al", nasm, StringComparison.Ordinal);
        Assert.Contains("movzx rax, al", nasm, StringComparison.Ordinal);
    }
}
