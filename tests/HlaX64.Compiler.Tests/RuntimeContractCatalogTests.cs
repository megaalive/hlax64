using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Verification;

namespace HlaX64.Compiler.Tests;

public sealed class RuntimeContractCatalogTests
{
    private static ProgramNode Parse(string source)
        => new Parser(new Lexer(source).Tokenize()).Parse();

    [Fact]
    public void ParseText_ReadsClobberListFromHeader()
    {
        const string nasm = """
            ; HLAX64-RUNTIME-FUNCTION v0.1
            ; name:    stdout_put_str
            ; target:  linux-x64-sysv
            ; clobbers:
            ;   rax, rcx, rdx, rsi, rdi
            ; preserves:
            ;   rbx, rbp, r12, r13, r14, r15
            global stdout_put_str
            stdout_put_str:
                ret
            """;

        var catalog = RuntimeContractCatalog.ParseText(nasm);
        Assert.True(catalog.TryGet("stdout_put_str", out var contract));
        Assert.Contains("rcx", contract.Clobbers);
        Assert.Contains("rbx", contract.Preserves);
        Assert.DoesNotContain("rbx", contract.Clobbers);
    }

    [Fact]
    public void Analyze_RuntimeContractCall_WarnsHlax0076()
    {
        const string nasm = """
            ; HLAX64-RUNTIME-FUNCTION v0.1
            ; name:    stdout_put_str
            ; clobbers:
            ;   rax, rcx, rdx, rsi, rdi
            ; preserves:
            ;   rbx, rbp
            """;
        var catalog = RuntimeContractCatalog.ParseText(nasm);

        var source = """
            program t;
            extern procedure stdout_put_str(s: cstring): int64;
            procedure P; @returns("rax");
            begin P;
                mov(42, rcx);
                call stdout_put_str("hi");
            end P;
            begin t;
            end t;
            """;

        var diags = new VerificationAnalyzer(new CompilerWarnings(Liveness: true), catalog)
            .Analyze(Parse(source));
        var warn = Assert.Single(diags.Diagnostics, d => d.Code == "HLAX0076");
        Assert.Contains("rcx", warn.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stdout_put_str", warn.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0063");
    }

    [Fact]
    public void Analyze_UnknownCall_StillUsesHlax0063()
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

        var empty = new RuntimeContractCatalog();
        var diags = new VerificationAnalyzer(new CompilerWarnings(Liveness: true), empty)
            .Analyze(Parse(source));
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0063");
        Assert.DoesNotContain(diags.Diagnostics, d => d.Code == "HLAX0076");
    }

    [Fact]
    public void LoadFromRuntimeRoot_FindsStdoutContracts()
    {
        var root = RuntimeContractCatalog.TryFindRuntimeRoot();
        Assert.False(string.IsNullOrWhiteSpace(root), "expected src/HlaX64.Runtime near repo");
        var catalog = RuntimeContractCatalog.LoadFromRuntimeRoot(root!);
        Assert.True(catalog.Count > 0);
        Assert.True(catalog.TryGet("stdout_put_str", out var contract));
        Assert.NotEmpty(contract.Clobbers);
    }
}
