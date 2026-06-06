using HlaX64.Compiler;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Tests;

/// <summary>
/// Ensures arbitrary input does not crash the compiler front-end.
/// </summary>
public class ParserRobustnessTests
{
    private static readonly Random Rng = new(42);

    [Theory]
    [InlineData("")]
    [InlineData("@@@")]
    [InlineData("program x; begin x; mov(1, raxz); end x;")]
    [InlineData("not a program at all")]
    [InlineData("procedure foo(); begin foo; end foo;")]
    public void ParseOrLex_ArbitraryInput_DoesNotThrow(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        try
        {
            _ = parser.Parse();
        }
        catch (ParseException)
        {
            // Expected for invalid syntax
        }
    }

    [Fact]
    public void Parse_RandomAsciiStrings_DoesNotThrowUnhandled()
    {
        for (int i = 0; i < 200; i++)
        {
            var len = Rng.Next(0, 256);
            var chars = new char[len];
            for (int j = 0; j < len; j++)
                chars[j] = (char)Rng.Next(32, 127);

            var source = new string(chars);
            var lexer = new Lexer(source);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            try
            {
                _ = parser.Parse();
            }
            catch (ParseException)
            {
            }
        }
    }

    [Fact]
    public void Compile_RandomAsciiStrings_DoesNotThrowUnhandled()
    {
        for (int i = 0; i < 100; i++)
        {
            var len = Rng.Next(0, 512);
            var chars = new char[len];
            for (int j = 0; j < len; j++)
                chars[j] = (char)Rng.Next(9, 127);

            var source = new string(chars);
            _ = new Compilation("(fuzz)", source).Process();
        }
    }
}
