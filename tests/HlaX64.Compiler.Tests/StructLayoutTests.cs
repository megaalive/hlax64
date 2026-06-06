using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Tests;

public sealed class StructLayoutTests
{
    [Fact]
    public void RecordLayout_NaturalAlignment_ComputesSizeAndOffsets()
    {
        var block = new RecordBlockNode("PatientHeader", new List<RecordFieldNode>
        {
            new("version", "uint16"),
            new("flags", "uint16"),
            new("length", "uint32"),
            new("timestamp", "uint64"),
        });

        var registry = new RecordTypeRegistry();
        Assert.True(registry.Register(block, out var record, out _));
        Assert.Equal(0, record.Fields[0].Offset);
        Assert.Equal(2, record.Fields[1].Offset);
        Assert.Equal(4, record.Fields[2].Offset);
        Assert.Equal(8, record.Fields[3].Offset);
        Assert.Equal(16, record.SizeInBytes);
    }

    [Fact]
    public void Semantic_SizeofAndOffsetof_Evaluates()
    {
        const string source = """
            program p;
            record PatientHeader
                version: uint16;
                length: uint32;
            endrecord;
            const
                HSize := sizeof(PatientHeader);
                LenOff := offsetof(PatientHeader, length);
            endconst;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var analyzer = new SemanticAnalyzer();
        var diags = analyzer.Analyze(program);
        Assert.False(diags.HasErrors);
        Assert.Equal(8, analyzer.ConstTable.Values["HSize"]);
        Assert.Equal(4, analyzer.ConstTable.Values["LenOff"]);
    }

    [Fact]
    public void Compile_RecordFieldAccess_LowersStackOffset()
    {
        const string source = """
            program p;
            record PatientHeader
                version: uint16;
                length: uint32;
            endrecord;
            procedure P; @returns("rax");
            var header: PatientHeader;
            begin P;
                mov(10, header.length);
            end P;
            begin p;
            end p;
            """;

        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var nasm = new HlaX64.Backend.Nasm.Emitters.NasmEmitter().Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("mov dword", nasm, StringComparison.Ordinal);
        Assert.Contains("+4]", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantic_UnknownRecordField_ReportsHlax0043()
    {
        const string source = """
            program p;
            record PatientHeader
                version: uint16;
            endrecord;
            procedure P; @returns("rax");
            var header: PatientHeader;
            begin P;
                mov(1, header.missing);
            end P;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0043");
    }

    [Fact]
    public void Semantic_InvalidOffsetof_ReportsHlax0044()
    {
        const string source = """
            program p;
            const
                X := offsetof(Unknown, field);
            endconst;
            begin p;
            end p;
            """;

        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.True(diags.HasErrors);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0044");
    }
}
