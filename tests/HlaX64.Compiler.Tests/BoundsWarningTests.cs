using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class BoundsWarningTests
{
    private static ProgramNode Parse(string source)
        => new Parser(new Lexer(source).Tokenize()).Parse();

    [Fact]
    public void Analyze_LiteralIndexOutOfBounds_WarnsWhenEnabled()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: int64[4];
            begin P;
                mov(buf[4], rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new SemanticAnalyzer(new CompilerWarnings(Bounds: true)).Analyze(Parse(source));
        var warn = Assert.Single(diags.Diagnostics, d => d.Code == "HLAX0030");
        Assert.Equal(DiagnosticSeverity.Warning, warn.Severity);
        Assert.Contains("out of bounds", warn.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_LiteralIndexInBounds_NoWarning()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: int64[4];
            begin P;
                mov(buf[3], rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new SemanticAnalyzer(new CompilerWarnings(Bounds: true)).Analyze(Parse(source));
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0030");
    }

    [Fact]
    public void Analyze_RegisterIndex_NoBoundsWarning()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var
                buf: int64[4];
                i: int64;
            begin P;
                mov(buf[i], rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new SemanticAnalyzer(new CompilerWarnings(Bounds: true)).Analyze(Parse(source));
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0030");
    }

    [Fact]
    public void Analyze_LiteralIndexOutOfBounds_NoWarningWhenDisabled()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: int64[4];
            begin P;
                mov(buf[9], rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new SemanticAnalyzer(new CompilerWarnings(Bounds: false)).Analyze(Parse(source));
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0030");
    }

    [Fact]
    public void Compilation_WithWbounds_IncludesWarningButSucceeds()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var buf: int64[2];
            begin P;
                mov(buf[2], rax);
            end P;
            begin t;
                mov(0, rax);
            end t;
            """;
        var options = CompilationOptions.Default with { Warnings = new CompilerWarnings(Bounds: true) };
        var result = new Compilation("(test)", source, options).Process();
        Assert.True(result.Success);
        Assert.Contains(result.StructuredDiagnostics, d => d.Code == "HLAX0030");
    }
}
