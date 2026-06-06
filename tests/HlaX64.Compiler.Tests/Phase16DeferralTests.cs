using HlaX64.Compiler;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Tests;

public sealed class Phase16DeferralTests
{
    [Fact]
    public void Enum_AutoIncrement_AssignsSequentialValues()
    {
        const string source = """
            program p;
            enum Color: uint32
                Red := 1;
                Green;
                Blue;
            endenum;
            begin p;
                mov(Color.Green, rax);
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var analyzer = new SemanticAnalyzer();
        var diags = analyzer.Analyze(program);
        Assert.False(diags.HasErrors);
        Assert.Equal(2, analyzer.ConstTable.Values["Color.Green"]);
        Assert.Equal(3, analyzer.ConstTable.Values["Color.Blue"]);
    }

    [Fact]
    public void Record_PackedLayout_HasNoPadding()
    {
        var block = new Ast.RecordBlockNode("Tight", new List<Ast.RecordFieldNode>
        {
            new("a", "int16"),
            new("b", "int32")
        }, isPacked: true);

        var registry = new RecordTypeRegistry();
        Assert.True(registry.Register(block, out var record, out _));
        Assert.Equal(0, record.Fields[0].Offset);
        Assert.Equal(2, record.Fields[1].Offset);
        Assert.Equal(6, record.SizeInBytes);
    }

    [Fact]
    public void ProcedureScopedEnum_ResolvesInBody()
    {
        const string source = """
            program p;
            procedure Main;
            enum Local: int32
                One := 10;
                Two;
            endenum;
            begin Main;
                mov(Local.Two, rax);
            end Main;
            begin p;
                call Main();
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void ProcedureScopedRecord_FieldAccess_Compiles()
    {
        const string source = """
            program p;
            procedure Main;
            record Pair
                a: int64;
                b: int64;
            endrecord;
            var
                p: Pair;
            begin Main;
                mov(7, p.a);
            end Main;
            begin p;
                call Main();
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }
}
