using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public class SemanticAnalyzerTests
{
    private ProgramNode ParseProgram(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        return parser.Parse();
    }

    [Fact]
    public void Analyze_ValidProgram_NoErrors()
    {
        var program = ParseProgram("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_UnknownRegister_ReportsError()
    {
        var program = ParseProgram("program test;\nbegin test;\n    mov(1, raxz);\nend test;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0012");
        Assert.Contains("raxz", error.Message);
        Assert.Equal("rax", error.Suggestion);
    }

    [Fact]
    public void Analyze_UnknownRegister_SuggestsClosest()
    {
        var program = ParseProgram("program test;\nbegin test;\n    mov(1, rcz);\nend test;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0012");
        Assert.Equal("rcx", error.Suggestion);
    }

    [Fact]
    public void Analyze_UnknownInstruction_ReportsError()
    {
        var program = ParseProgram("program test;\nbegin test;\n    moov(1, rax);\nend test;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0003");
        Assert.Contains("moov", error.Message);
    }

    [Fact]
    public void Analyze_InstructionWrongOperandCount_ReportsError()
    {
        var program = ParseProgram("program test;\nbegin test;\n    mov(rax);\nend test;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0004");
        Assert.Contains("2", error.Message);
    }

    [Fact]
    public void Analyze_DuplicateProcedure_ReportsError()
    {
        var source = "program test;\n" +
                     "procedure Foo();\nbegin Foo;\n    mov(1, rax);\nend Foo;\n" +
                     "procedure Foo();\nbegin Foo;\n    mov(2, rax);\nend Foo;\n" +
                     "begin test;\nend test;";
        var program = ParseProgram(source);
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0007");
        Assert.Contains("Foo", error.Message);
    }

    [Fact]
    public void Analyze_ValidProgramWithProcedure_NoErrors()
    {
        var source = "program test;\nprocedure AddTwo(arg1:int64; arg2:int64); @returns(\"rax\");\nbegin AddTwo;\n    mov(arg1, rax);\n    add(arg2, rax);\nend AddTwo;\nbegin test;\n    mov(1, rax);\nend test;";
        var program = ParseProgram(source);
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_InvalidRegisterInWhile_ReportsError()
    {
        var source = "program test;\nbegin test;\nwhile(rax < rdx) do\n    add(1, rcx);\nendwhile;\nend test;";
        var program = ParseProgram(source);
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_AllValidRegisters_NoErrors()
    {
        var program = ParseProgram("program test;\nbegin test;\n    mov(rax, rbx);\n    mov(ecx, edx);\n    mov(ax, bx);\n    mov(al, bl);\nend test;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Diagnostic_ToString_ContainsLocationAndSuggestion()
    {
        var diag = new Diagnostic("HLAX0012", DiagnosticSeverity.Error, "Unknown register 'raxz'", 3, 12, "rax");
        var str = diag.ToString();
        Assert.Contains("HLAX0012", str);
        Assert.Contains("line 3", str);
        Assert.Contains("column 12", str);
        Assert.Contains("Did you mean 'rax'?", str);
    }

    [Fact]
    public void Analyze_StdoutPut_Nl_NoErrors()
    {
        var program = ParseProgram("program hello;\n#include(\"stdlib64.hhf\")\nbegin hello;\n    stdout.put(\"Hello\", nl);\nend hello;");
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);
        Assert.False(diagnostics.HasErrors);
    }

    [Fact]
    public void Analyze_UnknownParameterType_ReportsError()
    {
        var program = ParseProgram("""
            program bad;
            procedure Bad(x:foobar); @returns("rax");
            begin Bad;
                mov(1, rax);
            end Bad;
            begin bad;
            end bad;
            """);
        var analyzer = new SemanticAnalyzer();
        var diagnostics = analyzer.Analyze(program);

        Assert.True(diagnostics.HasErrors);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0020");
        Assert.Contains("foobar", error.Message);
    }

    [Fact]
    public void Analyze_InvalidAddressOf_ReportsError()
    {
        var program = ParseProgram("""
            program bad;
            begin bad;
                mov(&rax, rcx);
            end bad;
            """);
        var diagnostics = new SemanticAnalyzer().Analyze(program);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0023");
        Assert.Contains("Address-of", error.Message);
    }

    [Fact]
    public void Analyze_MemoryRefNonRegister_ReportsError()
    {
        var program = ParseProgram("""
            program bad;
            begin bad;
                mov([42], rax);
            end bad;
            """);
        var diagnostics = new SemanticAnalyzer().Analyze(program);
        var error = Assert.Single(diagnostics.Diagnostics, d => d.Code == "HLAX0022");
        Assert.Contains("register", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Analyze_PointerLoadStore_NoErrors()
    {
        var program = ParseProgram("""
            program t;
            procedure P; @returns("rax");
            var slot: int64;
            begin P;
                mov(42, slot);
                mov(&slot, rcx);
                mov([rcx], rax);
            end P;
            begin t;
            end t;
            """);
        var diagnostics = new SemanticAnalyzer().Analyze(program);
        Assert.False(diagnostics.HasErrors);
    }
}