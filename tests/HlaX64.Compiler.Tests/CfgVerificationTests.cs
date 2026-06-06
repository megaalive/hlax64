using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Verification;

namespace HlaX64.Compiler.Tests;

public sealed class CfgVerificationTests
{
    private static ProgramNode Parse(string source)
        => new Parser(new Lexer(source).Tokenize()).Parse();

    [Fact]
    public void Analyze_UnreachableAfterRet_Warns()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            begin P;
                mov(0, rax);
                ret();
                mov(1, rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(Unreachable: true)).Analyze(Parse(source));
        var warn = Assert.Single(diags.Diagnostics, d => d.Code == "HLAX0061");
        Assert.Equal(DiagnosticSeverity.Warning, warn.Severity);
    }

    [Fact]
    public void Analyze_MissingReturnRegister_Warns()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            begin P;
                mov(0, rbx);
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(Unreachable: true)).Analyze(Parse(source));
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0062");
    }
}
