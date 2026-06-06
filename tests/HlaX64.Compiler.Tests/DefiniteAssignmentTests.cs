using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Verification;

namespace HlaX64.Compiler.Tests;

public sealed class DefiniteAssignmentTests
{
    private static ProgramNode Parse(string source)
        => new Parser(new Lexer(source).Tokenize()).Parse();

    private static CompilerWarnings VerifyAll => new(DefiniteAssignment: true, Unreachable: true, Liveness: true);

    [Fact]
    public void Analyze_InitBeforeRead_NoWarning()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                mov(1, x);
                mov(x, rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(VerifyAll).Analyze(Parse(source));
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0060");
    }

    [Fact]
    public void Analyze_ReadBeforeAssign_Warns()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                mov(x, rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(DefiniteAssignment: true)).Analyze(Parse(source));
        var warn = Assert.Single(diags.Diagnostics, d => d.Code == "HLAX0060");
        Assert.Equal(DiagnosticSeverity.Warning, warn.Severity);
    }

    [Fact]
    public void Analyze_IfElseBothAssign_NoWarningAfterMerge()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                mov(1, rax);
                if(rax > 0) then
                    mov(1, x);
                else
                    mov(2, x);
                endif;
                mov(x, rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(DefiniteAssignment: true)).Analyze(Parse(source));
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0060");
    }

    [Fact]
    public void Analyze_IfOnlyOneBranchAssign_WarnsOnReadAfterIf()
    {
        var source = """
            program t;
            procedure P; @returns("rax");
            var x: int64;
            begin P;
                mov(1, rax);
                if(rax > 0) then
                    mov(1, x);
                endif;
                mov(x, rax);
            end P;
            begin t;
            end t;
            """;
        var diags = new VerificationAnalyzer(new CompilerWarnings(DefiniteAssignment: true)).Analyze(Parse(source));
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0060");
    }
}
