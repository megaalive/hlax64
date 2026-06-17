using System.Globalization;
using System.Text;

namespace HlaX64.Compiler.Lexing;

public sealed class Lexer
{
    private readonly string _source;
    private int _pos;
    private int _line;
    private int _column;
    public Lexer(string source)
    {
        _source = source;
        _pos = 0;
        _line = 1;
        _column = 1;
    }

    public List<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < _source.Length)
        {
            var ch = Peek();

            // Skip whitespace (but track newlines)
            if (ch == ' ' || ch == '\t' || ch == '\r')
            {
                Advance();
                continue;
            }

            if (ch == '\n')
            {
                Advance();
                _line++;
                _column = 1;
                continue;
            }

            // Comments: // to end of line
            if (ch == '/' && _pos + 1 < _source.Length && _source[_pos + 1] == '/')
            {
                SkipLineComment();
                continue;
            }

            // Multi-line comments: /* ... */
            if (ch == '/' && _pos + 1 < _source.Length && _source[_pos + 1] == '*')
            {
                SkipBlockComment();
                continue;
            }

            var token = ScanToken();
            if (token.Type != TokenType.Unknown)
            {
                tokens.Add(token);
            }
        }

        tokens.Add(new Token(TokenType.EndOfFile, "", _line, _column));
        return tokens;
    }

    private Token ScanToken()
    {
        var ch = Peek();
        var line = _line;
        var col = _column;

        // String literals
        if (ch == '"')
        {
            return ScanStringLiteral();
        }

        // Hex literals: $FF
        if (ch == '$')
        {
            return ScanHexLiteral();
        }

        // Numbers
        if (char.IsDigit(ch))
        {
            return ScanIntegerLiteral();
        }

        // Identifiers and keywords
        if (char.IsLetter(ch) || ch == '_')
        {
            return ScanIdentifierOrKeyword();
        }

        // Symbols
        switch (ch)
        {
            case '(': Advance(); return CreateToken(TokenType.LeftParen, "(", line, col);
            case ')': Advance(); return CreateToken(TokenType.RightParen, ")", line, col);
            case ';': Advance(); return CreateToken(TokenType.Semicolon, ";", line, col);
            case ',': Advance(); return CreateToken(TokenType.Comma, ",", line, col);
            case '.': Advance(); return CreateToken(TokenType.Dot, ".", line, col);
            case '@': Advance(); return CreateToken(TokenType.At, "@", line, col);
            case '#': Advance(); return CreateToken(TokenType.Hash, "#", line, col);
            case ':':
                Advance();
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    Advance();
                    return CreateToken(TokenType.ColonAssign, ":=", line, col);
                }
                return CreateToken(TokenType.Colon, ":", line, col);
            case '+': Advance(); return CreateToken(TokenType.Plus, "+", line, col);
            case '-': Advance(); return CreateToken(TokenType.Minus, "-", line, col);
            case '*': Advance(); return CreateToken(TokenType.Star, "*", line, col);
            case '/': Advance(); return CreateToken(TokenType.Slash, "/", line, col);
            case '%': Advance(); return CreateToken(TokenType.Percent, "%", line, col);
            case '|': Advance(); return CreateToken(TokenType.Pipe, "|", line, col);
            case '^': Advance(); return CreateToken(TokenType.Caret, "^", line, col);
            case '~': Advance(); return CreateToken(TokenType.Tilde, "~", line, col);
            case '&': Advance(); return CreateToken(TokenType.Ampersand, "&", line, col);
            case '[': Advance(); return CreateToken(TokenType.LeftBracket, "[", line, col);
            case ']': Advance(); return CreateToken(TokenType.RightBracket, "]", line, col);

            case '=':
                Advance();
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    Advance();
                    return CreateToken(TokenType.DoubleEquals, "==", line, col);
                }
                return CreateToken(TokenType.Equals, "=", line, col);

            case '!':
                Advance();
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    Advance();
                    return CreateToken(TokenType.NotEquals, "!=", line, col);
                }
                return CreateToken(TokenType.Unknown, "!", line, col);

            case '<':
                Advance();
                if (_pos < _source.Length && _source[_pos] == '<')
                {
                    Advance();
                    return CreateToken(TokenType.ShiftLeft, "<<", line, col);
                }
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    Advance();
                    return CreateToken(TokenType.LessOrEqual, "<=", line, col);
                }
                if (_pos < _source.Length && _source[_pos] == '?')
                {
                    Advance();
                    return CreateToken(TokenType.LessThanUnsigned, "<?", line, col);
                }
                return CreateToken(TokenType.LessThan, "<", line, col);

            case '>':
                Advance();
                if (_pos < _source.Length && _source[_pos] == '>')
                {
                    Advance();
                    return CreateToken(TokenType.ShiftRight, ">>", line, col);
                }
                if (_pos < _source.Length && _source[_pos] == '=')
                {
                    Advance();
                    return CreateToken(TokenType.GreaterOrEqual, ">=", line, col);
                }
                if (_pos < _source.Length && _source[_pos] == '?')
                {
                    Advance();
                    return CreateToken(TokenType.GreaterThanUnsigned, ">?", line, col);
                }
                return CreateToken(TokenType.GreaterThan, ">", line, col);
        }

        // Unknown character
        Advance();
        return CreateToken(TokenType.Unknown, ch.ToString(), line, col);
    }

    private Token ScanStringLiteral()
    {
        var line = _line;
        var col = _column;
        var sb = new StringBuilder();
        Advance(); // skip opening quote

        while (_pos < _source.Length)
        {
            var ch = Peek();
            if (ch == '"')
            {
                Advance(); // skip closing quote
                break;
            }
            if (ch == '\\' && _pos + 1 < _source.Length)
            {
                Advance();
                var esc = Peek();
                switch (esc)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case '\\': sb.Append('\\'); break;
                    case '"': sb.Append('"'); break;
                    default: sb.Append(esc); break;
                }
                Advance();
                continue;
            }
            if (ch == '\n')
            {
                // Unterminated string — stop at newline
                break;
            }
            sb.Append(ch);
            Advance();
        }

        return CreateToken(TokenType.StringLiteral, sb.ToString(), line, col);
    }

    private Token ScanIntegerLiteral()
    {
        var line = _line;
        var col = _column;
        var sb = new StringBuilder();

        while (_pos < _source.Length && char.IsDigit(Peek()))
        {
            sb.Append(Peek());
            Advance();
        }

        return CreateToken(TokenType.IntegerLiteral, sb.ToString(), line, col);
    }

    private Token ScanHexLiteral()
    {
        var line = _line;
        var col = _column;
        Advance(); // skip $

        var sb = new StringBuilder();
        while (_pos < _source.Length && IsHexDigit(Peek()))
        {
            sb.Append(Peek());
            Advance();
        }

        if (sb.Length == 0)
            return CreateToken(TokenType.Unknown, "$", line, col);

        var value = long.Parse(sb.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return CreateToken(TokenType.IntegerLiteral, value.ToString(CultureInfo.InvariantCulture), line, col);
    }

    private static bool IsHexDigit(char ch)
        => char.IsDigit(ch) || ch is >= 'a' and <= 'f' or >= 'A' and <= 'F';

    private Token ScanIdentifierOrKeyword()
    {
        var line = _line;
        var col = _column;
        var sb = new StringBuilder();

        while (_pos < _source.Length && (char.IsLetterOrDigit(Peek()) || Peek() == '_'))
        {
            sb.Append(Peek());
            Advance();
        }

        var word = sb.ToString();

        // Check keywords and registers
        var type = word.ToLowerInvariant() switch
        {
            "program" => TokenType.Program,
            "begin" => TokenType.Begin,
            "end" => TokenType.End,
            "include" => TokenType.Include,
            "procedure" => TokenType.Procedure,
            "var" => TokenType.Var,
            "const" => TokenType.Const,
            "endconst" => TokenType.Endconst,
            "enum" => TokenType.Enum,
            "endenum" => TokenType.Endenum,
            "record" => TokenType.Record,
            "endrecord" => TokenType.Endrecord,
            "struct" => TokenType.Record,
            "endstruct" => TokenType.Endrecord,
            "static" => TokenType.Static,
            "endstatic" => TokenType.Endstatic,
            "packed" => TokenType.Packed,
            "cstring" => TokenType.Identifier,
            "utf8slice" => TokenType.Identifier,
            "export" => TokenType.Export,
            "extern" => TokenType.Extern,
            "type" => TokenType.Type,
            "from" => TokenType.From,
            "variadic" => TokenType.Variadic,
            "returns" => TokenType.Returns,
            "float32" => TokenType.Identifier,
            "float64" => TokenType.Identifier,
            "real32" => TokenType.Identifier,
            "real64" => TokenType.Identifier,
            "if" => TokenType.If,
            "then" => TokenType.Then,
            "else" => TokenType.Else,
            "endif" => TokenType.Endif,
            "while" => TokenType.While,
            "do" => TokenType.Do,
            "endwhile" => TokenType.Endwhile,
            "break" => TokenType.Break,
            "continue" => TokenType.Continue,
            "pragma" => TokenType.Pragma,

            // 64-bit registers
            "rax" => TokenType.RAX,
            "rbx" => TokenType.RBX,
            "rcx" => TokenType.RCX,
            "rdx" => TokenType.RDX,
            "rsi" => TokenType.RSI,
            "rdi" => TokenType.RDI,
            "rbp" => TokenType.RBP,
            "rsp" => TokenType.RSP,
            "r8" => TokenType.R8,
            "r9" => TokenType.R9,
            "r10" => TokenType.R10,
            "r11" => TokenType.R11,
            "r12" => TokenType.R12,
            "r13" => TokenType.R13,
            "r14" => TokenType.R14,
            "r15" => TokenType.R15,

            // 32-bit registers
            "eax" => TokenType.EAX,
            "ebx" => TokenType.EBX,
            "ecx" => TokenType.ECX,
            "edx" => TokenType.EDX,
            "esi" => TokenType.ESI,
            "edi" => TokenType.EDI,
            "ebp" => TokenType.EBP,
            "esp" => TokenType.ESP,
            "r8d" => TokenType.R8D,
            "r9d" => TokenType.R9D,
            "r10d" => TokenType.R10D,
            "r11d" => TokenType.R11D,
            "r12d" => TokenType.R12D,
            "r13d" => TokenType.R13D,
            "r14d" => TokenType.R14D,
            "r15d" => TokenType.R15D,

            // 16-bit registers
            "ax" => TokenType.AX,
            "bx" => TokenType.BX,
            "cx" => TokenType.CX,
            "dx" => TokenType.DX,

            // 8-bit registers
            "al" => TokenType.AL,
            "bl" => TokenType.BL,
            "cl" => TokenType.CL,
            "dl" => TokenType.DL,

            _ => TokenType.Identifier
        };

        return CreateToken(type, word, line, col);
    }

    private void SkipLineComment()
    {
        while (_pos < _source.Length && Peek() != '\n')
        {
            Advance();
        }
    }

    private void SkipBlockComment()
    {
        Advance(); // /
        Advance(); // *
        while (_pos + 1 < _source.Length)
        {
            if (Peek() == '*' && _source[_pos + 1] == '/')
            {
                Advance(); // *
                Advance(); // /
                return;
            }
            if (Peek() == '\n')
            {
                _line++;
                _column = 1;
            }
            Advance();
        }
    }

    private char Peek()
    {
        return _source[_pos];
    }

    private void Advance()
    {
        _pos++;
        _column++;
    }

    private static Token CreateToken(TokenType type, string value, int line, int column)
    {
        return new Token(type, value, line, column);
    }
}