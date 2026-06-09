using HlaX64.Compiler;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Tests;

public sealed class ParserRecoveryTests
{
    [Fact]
    public void Parse_ExternProcedureSignatureErrors_reports_multiple_diagnostics()
    {
        var source = """
            program procview;
            begin procview;

            extern procedure CreateFileAaaaaaa(
                lpFileName: cstring
                dwDesiredAccess: uint64;
                dwShareMode: uint64 asdasdasda;
                lpSecurityAttributes: ptr;
                dwCreationDisposition: uint64;
                dwFlagsAndAttributes: uint64;
                hTemplateFile: uint64 asdadasdadsdasdsad
            ): ptr from "kernel32.dll";

            extern procedure aaaReadFile(
                hFile: ptr;
                lpBuffer: ptr;
                nNumberOfBytesToRead: uint64;
                lpNumberOfBytesRead: ptr;
                lpOverlapped: ptrsdasdasdasdasd
            ): int32 from "kernel32.dll";

            end procview;
            """;

        var parser = new Parser(new Lexer(source).Tokenize());
        _ = parser.Parse();

        Assert.True(parser.HasErrors);
        Assert.True(parser.Diagnostics.Count >= 4, string.Join('\n', parser.Diagnostics.Select(d => d.ToString())));

        var result = new Compilation("(test)", source).Process();
        Assert.False(result.Success);
        Assert.True(result.StructuredDiagnostics.Count >= 4);
    }

    [Fact]
    public void Parse_StatementBodyErrors_reports_multiple_diagnostics()
    {
        var source = """
            program hello;
            begin hello;
            asdasdasdasd
                mov(1, raxz);
                mov(2, rbxz);
            end hello;
            """;

        var result = new Compilation("(test)", source).Process();

        Assert.False(result.Success);
        Assert.Contains(result.StructuredDiagnostics, d => d.Code == "HLAX1000");
        Assert.Contains(result.StructuredDiagnostics, d => d.Code == "HLAX0012" && d.Message.Contains("raxz"));
        Assert.Contains(result.StructuredDiagnostics, d => d.Code == "HLAX0012" && d.Message.Contains("rbxz"));
    }
}
