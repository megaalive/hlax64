using HlaX64.Compiler.Formatting;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Tests;

public class AstFormatterTests
{
    [Fact]
    public void Format_ProcedureProgram_ProducesCanonicalLayout()
    {
        const string source = """
            program main;
            procedure AddTwo(a:int64; b:int64); @returns("rax");
            begin AddTwo;
            mov(a,rax);
            end AddTwo;
            begin main;
            mov(1,rax);
            end main;
            """;

        var formatted = AstFormatter.Format(source);
        Assert.Contains("procedure AddTwo(a:int64; b:int64); @returns(\"rax\");", formatted);
        Assert.Contains("    mov(a, rax);", formatted);
        Assert.Contains("begin main;", formatted);
    }

    [Fact]
    public void Format_InvalidSource_ThrowsParseException()
    {
        Assert.Throws<ParseException>(() => AstFormatter.Format("begin x;\nend x;"));
    }
}
