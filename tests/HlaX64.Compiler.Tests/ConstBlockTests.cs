using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class ConstBlockTests
{
    [Fact]
    public void Parse_ProgramConstBlock_ParsesDeclarations()
    {
        const string source = """
            program p;
            const
                BufferSize := 4096;
                PageMask := $FF;
            endconst;
            begin p;
                mov(BufferSize, rax);
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        Assert.Single(program.Constants);
        var block = Assert.IsType<ConstBlockNode>(program.Constants[0]);
        Assert.Equal(2, block.Declarations.Count);
        Assert.Equal("BufferSize", block.Declarations[0].Name);
        Assert.IsType<IntegerLiteralNode>(block.Declarations[0].Expression);
    }

    [Fact]
    public void Parse_ProcedureConstExpression_ParsesBinaryExpr()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            const
                PageSize := 4096;
                PageMask := PageSize - 1;
            endconst;
            begin P;
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        var block = Assert.IsType<ConstBlockNode>(proc.Constants[0]);
        var mask = block.Declarations[1];
        var expr = Assert.IsType<BinaryExprNode>(mask.Expression);
        Assert.Equal("-", expr.Operator);
        Assert.IsType<IdentifierNode>(expr.Left);
        Assert.IsType<IntegerLiteralNode>(expr.Right);
    }

    [Fact]
    public void Semantic_ConstExpression_EvaluatesAndAllowsUse()
    {
        const string source = """
            program p;
            const
                Mask := (16 * 4) - 1;
            endconst;
            begin p;
                mov(Mask, rax);
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var analyzer = new SemanticAnalyzer();
        var diags = analyzer.Analyze(program);
        Assert.False(diags.HasErrors);
        Assert.Equal(63, analyzer.ConstTable.Values["Mask"]);
    }

    [Fact]
    public void Semantic_ArraySizeFromConst_ResolvesElementCount()
    {
        const string source = """
            program p;
            procedure P; @returns("rax");
            const
                N := 4;
            endconst;
            var buf: byte[N];
            begin P;
                mov(0, buf[0]);
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.False(diags.HasErrors);
        var proc = (ProcedureNode)program.Statements[0];
        var varNode = (VariableNode)proc.Variables[0];
        Assert.Equal(4, varNode.ElementCount);
    }

    [Fact]
    public void Compile_ConstOperand_LowersToImmediate()
    {
        const string source = """
            program p;
            const
                Answer := 42;
            endconst;
            begin p;
                mov(Answer, rax);
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter().Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("mov rax, 42", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_UndefinedConst_ReportsHlax0031()
    {
        const string source = """
            program p;
            const
                X := Missing + 1;
            endconst;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0031");
    }

    [Fact]
    public void Semantic_DivideByZero_ReportsHlax0032()
    {
        const string source = """
            program p;
            const
                X := 1 / 0;
            endconst;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0032");
    }

    [Fact]
    public void Semantic_DuplicateConst_ReportsHlax0034()
    {
        const string source = """
            program p;
            const
                N := 1;
                N := 2;
            endconst;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0034");
    }
}
