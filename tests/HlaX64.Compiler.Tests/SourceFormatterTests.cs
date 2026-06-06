using HlaX64.Cli.Formatting;

namespace HlaX64.Compiler.Tests;

public class SourceFormatterTests
{
    [Fact]
    public void Format_TrimsTrailingWhitespace()
    {
        var input = "program t;\r\nbegin t;  \r\n    mov(1, rax);   \r\nend t;\r\n";
        var output = SourceFormatter.Format(input);
        Assert.DoesNotContain("   \n", output);
    }

    [Fact]
    public void Format_IndentsBeginBlock()
    {
        var input = "program t;\nbegin t;\nmov(1, rax);\nend t;\n";
        var output = SourceFormatter.Format(input);
        Assert.Contains("    mov(1, rax);", output);
    }
}
