using System.Text;
using System.Text.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Formatting;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Tests;

/// <summary>Phase 18 expanded fuzzing: lexer, formatter round-trip, manifest JSON.</summary>
public sealed class FuzzTests
{
    private static readonly Random Rng = new(99);

    private static readonly string[] ValidFixtures =
    [
        """
        program t;
        procedure P; @returns("rax");
        var x: int64;
        begin P;
            mov(1, x);
            mov(x, rax);
        end P;
        begin t;
            mov(0, rax);
        end t;
        """,
        """
        program hello;
        begin hello;
            mov(42, rax);
        end hello;
        """,
        """
        program t;
        procedure Add(a:int64; b:int64); @returns("rax");
        begin Add;
            mov(a, rax);
            add(b, rax);
        end Add;
        begin t;
            mov(0, rax);
        end t;
        """
    ];

    [Fact]
    public void Lexer_RandomUtf8_DoesNotThrow()
    {
        for (int i = 0; i < 150; i++)
        {
            var bytes = new byte[Rng.Next(0, 128)];
            Rng.NextBytes(bytes);
            var text = Encoding.UTF8.GetString(bytes);
            _ = new Lexer(text).Tokenize();
        }
    }

    [Fact]
    public void Formatter_ParseFormatRoundTrip_ValidFixtures()
    {
        foreach (var source in ValidFixtures)
        {
            var formatted = AstFormatter.Format(source);
            var reparsed = new Parser(new Lexer(formatted).Tokenize()).Parse();
            var original = new Parser(new Lexer(source).Tokenize()).Parse();
            Assert.Equal(original.Statements.Count, reparsed.Statements.Count);
        }
    }

    [Fact]
    public void ManifestJson_RandomObjects_ParseOrFailGracefully()
    {
        for (int i = 0; i < 100; i++)
        {
            var obj = new Dictionary<string, object?>
            {
                ["source"] = Rng.Next(2) == 0 ? "main.hla64" : null,
                ["expectExitCode"] = Rng.Next(-128, 128),
                ["expectCodes"] = Rng.Next(2) == 0 ? new[] { "HLAX0001" } : null
            };
            var json = JsonSerializer.Serialize(obj);
            try
            {
                _ = JsonSerializer.Deserialize<TestManifest>(json);
            }
            catch (JsonException)
            {
                // acceptable for malformed fuzz input
            }
        }
    }
}
