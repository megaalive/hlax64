using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.LanguageServer;

namespace HlaX64.Compiler.Tests;

public sealed class LanguageServerEditorServicesTests
{
    [Fact]
    public void GetHover_OnMnemonic_ReturnsMarkdown()
    {
        var source = "program t;\nbegin t;\n    mov(1, rax);\nend t;";
        var hover = LanguageServerEditorServices.GetHover(source, 2, 5);
        Assert.NotNull(hover);
    }

    [Fact]
    public void GetCompletions_IncludesMovAndRax()
    {
        var result = LanguageServerEditorServices.GetCompletions("program t;\nbegin t;\n    mo", 2, 6);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        Assert.Contains("mov", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DocumentDiagnostics_ParseError_UsesLineFromException()
    {
        var source = "program t;\nbegin t;\n    mov(&rax, rcx);\nend t;";
        var diags = DocumentDiagnostics.ToLsp("file:///test.hla64", source);
        var first = Assert.IsAssignableFrom<object[]>(diags).First();
        var json = System.Text.Json.JsonSerializer.Serialize(first);
        Assert.Contains("HLAX0023", json);
    }

    [Fact]
    public void Parse_ArrayDeclaration_ParsesElementCount()
    {
        var source = "program t;\nprocedure P; @returns(\"rax\");\nvar a: int64[4];\nbegin P;\nend P;\nbegin t;\nend t;";
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        var varNode = Assert.IsType<VariableNode>(proc.Variables[0]);
        Assert.Equal(4, varNode.ElementCount);
    }

    [Fact]
    public void GetDefinition_OnProcedure_ReturnsLocation()
    {
        var source = """
            program t;
            procedure Fill; @returns("rax");
            var buf: int64[4];
            begin Fill;
                mov(0, rax);
            end Fill;
            begin t;
                call Fill();
            end t;
            """;
        var def = LanguageServerEditorServices.GetDefinition(source, 7, 10, "file:///test.hla64");
        Assert.NotNull(def);
        var json = System.Text.Json.JsonSerializer.Serialize(def);
        Assert.Contains("file:///test.hla64", json);
        Assert.Contains("range", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetDocumentSymbols_ListsProcedureAndVars()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                mov(1, rax);
            end P;
            begin t;
            end t;
            """;
        var symbols = LanguageServerEditorServices.GetDocumentSymbols(source);
        var json = System.Text.Json.JsonSerializer.Serialize(symbols);
        Assert.Contains("P", json);
        Assert.Contains("x", json);
    }

    [Fact]
    public void FormatDocument_NormalizesLayout()
    {
        var source = "program t;\nbegin t;\nmov(1,rax);\nend t;";
        var edits = LanguageServerEditorServices.FormatDocument(source);
        Assert.NotNull(edits);
        var json = System.Text.Json.JsonSerializer.Serialize(edits);
        Assert.Contains("mov(1, rax)", json);
    }

    [Fact]
    public void DocumentDiagnostics_BoundsWarning_WhenLiteralOutOfRange()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: int64[2];
            begin P;
                mov(buf[5], rax);
            end P;
            begin t;
            end t;
            """;
        var diags = DocumentDiagnostics.ToLsp("file:///test.hla64", source);
        var json = System.Text.Json.JsonSerializer.Serialize(diags);
        Assert.Contains("HLAX0030", json);
    }

    [Fact]
    public void Emit_ArrayLiteralIndex_UsesScaledOffset()
    {
        var source = @"program t;
procedure P; @returns(""rax"");
var vals: int64[3];
begin P;
    mov(20, vals[1]);
    mov(vals[1], rax);
end P;
begin t;
    mov(0, rax);
end t;";
        var compilation = new Compilation("(test)", source);
        var result = compilation.Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var emitter = new HlaX64.Backend.Nasm.Emitters.NasmEmitter();
        var nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals);
        Assert.Contains("+8]", nasm);
    }
}
