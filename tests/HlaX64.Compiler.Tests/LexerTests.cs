using HlaX64.Compiler.Lexing;

namespace HlaX64.Compiler.Tests;

public class LexerTests
{
    [Fact]
    public void Tokenize_EmptySource_ReturnsOnlyEOF()
    {
        var lexer = new Lexer("");
        var tokens = lexer.Tokenize();
        Assert.Single(tokens);
        Assert.Equal(TokenType.EndOfFile, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_ProgramKeyword_ReturnsProgramToken()
    {
        var lexer = new Lexer("program");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.Program, tokens[0].Type);
        Assert.Equal("program", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_BeginKeyword_ReturnsBeginToken()
    {
        var lexer = new Lexer("begin");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.Begin, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_EndKeyword_ReturnsEndToken()
    {
        var lexer = new Lexer("end");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.End, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_IncludeDirective_ReturnsHashAndInclude()
    {
        var lexer = new Lexer("#include");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.Hash, tokens[0].Type);
        Assert.Equal(TokenType.Include, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_Registers_ReturnsRegisterTokens()
    {
        var lexer = new Lexer("rax rbx rcx rdx rsi rdi rbp rsp r8 r15 eax ebx r8d ax al");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.RAX, tokens[0].Type);
        Assert.Equal(TokenType.RBX, tokens[1].Type);
        Assert.Equal(TokenType.RCX, tokens[2].Type);
        Assert.Equal(TokenType.RDX, tokens[3].Type);
        Assert.Equal(TokenType.RSI, tokens[4].Type);
        Assert.Equal(TokenType.RDI, tokens[5].Type);
        Assert.Equal(TokenType.RBP, tokens[6].Type);
        Assert.Equal(TokenType.RSP, tokens[7].Type);
        Assert.Equal(TokenType.R8, tokens[8].Type);
        Assert.Equal(TokenType.R15, tokens[9].Type);
        Assert.Equal(TokenType.EAX, tokens[10].Type);
        Assert.Equal(TokenType.EBX, tokens[11].Type);
        Assert.Equal(TokenType.R8D, tokens[12].Type);
        Assert.Equal(TokenType.AX, tokens[13].Type);
        Assert.Equal(TokenType.AL, tokens[14].Type);
    }

    [Fact]
    public void Tokenize_IntegerLiteral_ReturnsIntegerToken()
    {
        var lexer = new Lexer("42");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.IntegerLiteral, tokens[0].Type);
        Assert.Equal("42", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_StringLiteral_ReturnsStringToken()
    {
        var lexer = new Lexer("\"hello\"");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.StringLiteral, tokens[0].Type);
        Assert.Equal("hello", tokens[0].Value);
    }

    [Fact]
    public void Tokenize_Symbols_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("();,.:@#=> <");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.LeftParen, tokens[0].Type);
        Assert.Equal(TokenType.RightParen, tokens[1].Type);
        Assert.Equal(TokenType.Semicolon, tokens[2].Type);
        Assert.Equal(TokenType.Comma, tokens[3].Type);
        Assert.Equal(TokenType.Dot, tokens[4].Type);
        Assert.Equal(TokenType.Colon, tokens[5].Type);
        Assert.Equal(TokenType.At, tokens[6].Type);
        Assert.Equal(TokenType.Hash, tokens[7].Type);
        Assert.Equal(TokenType.Equals, tokens[8].Type);
        Assert.Equal(TokenType.GreaterThan, tokens[9].Type);
        Assert.Equal(TokenType.LessThan, tokens[10].Type);
    }

    [Fact]
    public void Tokenize_LineComment_SkipsToEndOfLine()
    {
        var lexer = new Lexer("mov(1, rax); // this is a comment\nadd(2, rax);");
        var tokens = lexer.Tokenize();
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "mov");
        Assert.Contains(tokens, t => t.Type == TokenType.Identifier && t.Value == "add");
        Assert.DoesNotContain(tokens, t => t.Value == "this is a comment");
    }

    [Fact]
    public void Tokenize_ControlFlowKeywords_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("if then else endif while do endwhile");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.If, tokens[0].Type);
        Assert.Equal(TokenType.Then, tokens[1].Type);
        Assert.Equal(TokenType.Else, tokens[2].Type);
        Assert.Equal(TokenType.Endif, tokens[3].Type);
        Assert.Equal(TokenType.While, tokens[4].Type);
        Assert.Equal(TokenType.Do, tokens[5].Type);
        Assert.Equal(TokenType.Endwhile, tokens[6].Type);
    }

    [Fact]
    public void Tokenize_ProcedureKeywords_ReturnsCorrectTokens()
    {
        var lexer = new Lexer("procedure var export returns pragma");
        var tokens = lexer.Tokenize();
        Assert.Equal(TokenType.Procedure, tokens[0].Type);
        Assert.Equal(TokenType.Var, tokens[1].Type);
        Assert.Equal(TokenType.Export, tokens[2].Type);
        Assert.Equal(TokenType.Returns, tokens[3].Type);
        Assert.Equal(TokenType.Pragma, tokens[4].Type);
    }

    [Fact]
    public void Tokenize_LineAndColumn_TrackedCorrectly()
    {
        var lexer = new Lexer("program\nbegin\n    mov(1, rax);\nend;");
        var tokens = lexer.Tokenize();
        
        var programToken = tokens[0];
        Assert.Equal(1, programToken.Line);
        
        var beginToken = tokens[1];
        Assert.Equal(2, beginToken.Line);
        
        var movToken = tokens[2];
        Assert.Equal(3, movToken.Line);
    }
}