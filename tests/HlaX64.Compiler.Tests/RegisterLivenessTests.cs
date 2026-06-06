using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Verification;

namespace HlaX64.Compiler.Tests;

public sealed class RegisterLivenessTests
{
    private static ProgramNode Parse(string source)
        => new Parser(new Lexer(source).Tokenize()).Parse();

    [Fact]
    public void Analyze_LiveRcxAcrossCall_Warns()
    {
        var source = """
            program t;
            procedure Other; @returns("rax");
            begin Other;
                mov(0, rax);
            end Other;
            procedure P; @returns("rax");
            begin P;
                mov(42, rcx);
                call Other();
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(Liveness: true)).Analyze(Parse(source));
        var warn = Assert.Single(diags.Diagnostics, d => d.Code == "HLAX0063");
        Assert.Contains("rcx", warn.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_CallClearsLiveState_NoSecondWarningOnNextCall()
    {
        var source = """
            program t;
            procedure A; @returns("rax");
            begin A;
                mov(0, rax);
            end A;
            procedure B; @returns("rax");
            begin B;
                mov(0, rax);
            end B;
            procedure P; @returns("rax");
            begin P;
                mov(42, rcx);
                call A();
                call B();
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(Liveness: true)).Analyze(Parse(source));
        Assert.Single(diags.Diagnostics, d => d.Code == "HLAX0063");
    }
}
