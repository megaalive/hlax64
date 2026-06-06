using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class EnumTests
{
    [Fact]
    public void Parse_EnumBlock_ParsesMembers()
    {
        const string source = """
            program p;
            enum Color: uint32
                Red := 1;
                Green := 2;
            endenum;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        Assert.Single(program.Enums);
        var block = Assert.IsType<EnumBlockNode>(program.Enums[0]);
        Assert.Equal("Color", block.Name);
        Assert.Equal("uint32", block.BackingType);
        Assert.Equal(2, block.Members.Count);
    }

    [Fact]
    public void Semantic_EnumMember_ResolvesInConstTable()
    {
        const string source = """
            program p;
            enum Color: uint32
                Red := 1;
                Blue := 3;
            endenum;
            begin p;
                mov(Color.Red, rax);
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var analyzer = new SemanticAnalyzer();
        var diags = analyzer.Analyze(program);
        Assert.False(diags.HasErrors);
        Assert.Equal(1, analyzer.ConstTable.Values["Color.Red"]);
        Assert.Equal(3, analyzer.ConstTable.Values["Color.Blue"]);
    }

    [Fact]
    public void Compile_EnumImmediate_LowersToConstant()
    {
        const string source = """
            program p;
            enum Color: uint32
                Red := 1;
            endenum;
            begin p;
                mov(Color.Red, rax);
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter().Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("mov rax, 1", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_DuplicateEnumMember_ReportsHlax0039()
    {
        const string source = """
            program p;
            enum Color: uint32
                Red := 1;
                Red := 2;
            endenum;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0039");
    }

    [Fact]
    public void Semantic_InvalidEnumBackingType_ReportsHlax0040()
    {
        const string source = """
            program p;
            enum Color: byte
                Red := 1;
            endenum;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0040");
    }

    [Fact]
    public void Semantic_UndefinedEnumMember_ReportsHlax0041()
    {
        const string source = """
            program p;
            enum Color: uint32
                Red := 1;
            endenum;
            begin p;
                mov(Color.Magenta, rax);
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0041");
    }
}
