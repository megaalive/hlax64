using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;

namespace HlaX64.Compiler.Parsing;

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
        _pos = 0;
    }

    public ProgramNode Parse()
    {
        // program name;
        Expect(TokenType.Program);
        var programName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Semicolon);

        // #include directives before begin
        var includes = new List<AstNode>();
        while (Peek().Type == TokenType.Hash)
        {
            includes.Add(ParseInclude());
        }

        // Optional procedure declarations before begin
        var procedures = new List<AstNode>();
        while (Peek().Type == TokenType.Procedure || Peek().Type == TokenType.Export)
        {
            procedures.Add(ParseProcedure());
        }

        // begin name;
        Expect(TokenType.Begin);
        var beginName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Semicolon);

        // Statements
        var statements = new List<AstNode>();
        while (Peek().Type != TokenType.End && Peek().Type != TokenType.EndOfFile)
        {
            statements.AddRange(ParseStatementsUntilEnd());
        }

        // end name;
        Expect(TokenType.End);
        var endName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Semicolon);

        // Combine procedures + main statements into the program
        var allStatements = new List<AstNode>();
        allStatements.AddRange(procedures);
        allStatements.AddRange(statements);

        return new ProgramNode(programName, includes, allStatements);
    }

    private List<AstNode> ParseStatementsUntilEnd()
    {
        var stmts = new List<AstNode>();

        while (_pos < _tokens.Count)
        {
            var token = Peek();

            if (token.Type == TokenType.End || token.Type == TokenType.EndOfFile)
                break;

            if (token.Type == TokenType.Endif || token.Type == TokenType.Endwhile)
                break;

            stmts.Add(ParseStatement());
        }

        return stmts;
    }

    private AstNode ParseStatement()
    {
        var token = Peek();

        // Control flow
        if (token.Type == TokenType.If)
            return ParseIf();

        if (token.Type == TokenType.While)
            return ParseWhile();

        if (token.Type == TokenType.Procedure || token.Type == TokenType.Export)
            return ParseProcedure();

        // "call" is treated as a soft keyword: call ProcName(arg1, arg2); or call ProcName();
        if (token.Type == TokenType.Identifier && token.Value == "call" &&
            _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Identifier)
        {
            Advance(); // skip "call"
            var procName = Expect(TokenType.Identifier).Value;
            var callArgs = new List<AstNode>();

            // Support both call ProcName(); and call ProcName;
            if (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.LeftParen)
            {
                Advance(); // skip (
                while (_pos < _tokens.Count && Peek().Type != TokenType.RightParen)
                {
                    callArgs.Add(ParseOperand());
                    if (Peek().Type == TokenType.Comma)
                        Advance();
                }
                if (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.RightParen)
                    Advance(); // skip )
            }

            Expect(TokenType.Semicolon);
            return new CallNode(procName, callArgs);
        }

        // stdout.put(...) — function call with dot
        if (token.Type == TokenType.Identifier && _pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Dot)
        {
            return ParseCallWithDot();
        }

        // call AddTwo(...)
        if (token.Type == TokenType.Identifier && _pos + 1 < _tokens.Count && 
            (_tokens[_pos + 1].Type == TokenType.LeftParen || 
             (_pos + 2 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.Dot && _tokens[_pos + 2].Type == TokenType.LeftParen)))
        {
            return ParseCallOrInstruction();
        }

        // Instruction like mov(1, rax), add(2, rcx), xor(rax, rax)
        return ParseInstruction();
    }

    private AstNode ParseInclude()
    {
        Expect(TokenType.Hash); // skip #
        Expect(TokenType.Include);
        Expect(TokenType.LeftParen);
        var path = Expect(TokenType.StringLiteral).Value;
        Expect(TokenType.RightParen);
        return new IncludeNode(path);
    }

    private AstNode ParseProcedure()
    {
        var isExport = false;
        if (Peek().Type == TokenType.Export)
        {
            isExport = true;
            Advance();
        }

        Expect(TokenType.Procedure);
        var name = Expect(TokenType.Identifier).Value;

        var parameters = new List<ParameterNode>();
        if (_pos < _tokens.Count && _tokens[_pos].Type == TokenType.LeftParen)
        {
            Advance(); // skip (
            if (Peek().Type != TokenType.RightParen)
            {
                parameters.Add(ParseParameter());
                while (Peek().Type == TokenType.Semicolon)
                {
                    Advance();
                    parameters.Add(ParseParameter());
                }
            }
            Expect(TokenType.RightParen);
        }
        Expect(TokenType.Semicolon);

        // @returns("rax")
        string? returnsRegister = null;
        if (Peek().Type == TokenType.At)
        {
            Advance(); // skip @
            Expect(TokenType.Returns);
            Expect(TokenType.LeftParen);
            returnsRegister = Expect(TokenType.StringLiteral).Value;
            Expect(TokenType.RightParen);
            Expect(TokenType.Semicolon);
        }

        // Optional var declarations
        var variables = new List<AstNode>();
        if (Peek().Type == TokenType.Var)
        {
            Advance(); // skip var
            while (Peek().Type == TokenType.Identifier)
            {
                var varToken = Expect(TokenType.Identifier);
                Expect(TokenType.Colon);
                var typeToken = Expect(TokenType.Identifier);
                var elementCount = 1;
                if (Peek().Type == TokenType.LeftBracket)
                {
                    Advance();
                    var countToken = Expect(TokenType.IntegerLiteral);
                    elementCount = int.Parse(countToken.Value);
                    Expect(TokenType.RightBracket);
                }

                Expect(TokenType.Semicolon);
                variables.Add(WithLocation(new VariableNode(varToken.Value, typeToken.Value, elementCount), varToken));
            }
        }

        Expect(TokenType.Begin);
        var bodyName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Semicolon);

        var body = new List<AstNode>();
        while (Peek().Type != TokenType.End && Peek().Type != TokenType.EndOfFile)
        {
            body.AddRange(ParseStatementsUntilEnd());
            if (Peek().Type == TokenType.End)
                break;
        }

        Expect(TokenType.End);
        var endName = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Semicolon);

        return new ProcedureNode(name, parameters, returnsRegister, isExport, variables, body);
    }

    private ParameterNode ParseParameter()
    {
        var name = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Colon);
        var type = Expect(TokenType.Identifier).Value;
        return new ParameterNode(name, type);
    }

    private AstNode ParseIf()
    {
        Expect(TokenType.If);
        Expect(TokenType.LeftParen);
        var condition = ParseComparison();
        Expect(TokenType.RightParen);
        Expect(TokenType.Then);

        var thenBody = new List<AstNode>();
        while (Peek().Type != TokenType.Else && Peek().Type != TokenType.Endif && Peek().Type != TokenType.EndOfFile)
        {
            thenBody.Add(ParseStatement());
        }

        var elseBody = new List<AstNode>();
        if (Peek().Type == TokenType.Else)
        {
            Advance(); // skip else
            while (Peek().Type != TokenType.Endif && Peek().Type != TokenType.EndOfFile)
            {
                elseBody.Add(ParseStatement());
            }
        }

        Expect(TokenType.Endif);
        Expect(TokenType.Semicolon);
        return new IfNode(condition, thenBody, elseBody);
    }

    private AstNode ParseWhile()
    {
        Expect(TokenType.While);
        Expect(TokenType.LeftParen);
        var condition = ParseComparison();
        Expect(TokenType.RightParen);
        Expect(TokenType.Do);

        var body = new List<AstNode>();
        while (Peek().Type != TokenType.Endwhile && Peek().Type != TokenType.EndOfFile)
        {
            body.Add(ParseStatement());
        }

        Expect(TokenType.Endwhile);
        Expect(TokenType.Semicolon);
        return new WhileNode(condition, body);
    }

    private AstNode ParseComparison()
    {
        var left = ParseOperand();
        var op = Peek();

        if (op.Type == TokenType.Equals || op.Type == TokenType.LessThan || op.Type == TokenType.GreaterThan ||
            op.Type == TokenType.LessThanUnsigned || op.Type == TokenType.GreaterThanUnsigned)
        {
            Advance();
            var right = ParseOperand();
            return new ComparisonNode(left, op.Value, right);
        }

        return left;
    }

    private AstNode ParseCallWithDot()
    {
        var name = Expect(TokenType.Identifier).Value;
        Expect(TokenType.Dot);
        var method = Expect(TokenType.Identifier).Value;
        Expect(TokenType.LeftParen);
        var args = new List<AstNode>();

        if (Peek().Type != TokenType.RightParen)
        {
            args.Add(ParseOperand());
            while (Peek().Type == TokenType.Comma)
            {
                Advance();
                args.Add(ParseOperand());
            }
        }
        Expect(TokenType.RightParen);
        Expect(TokenType.Semicolon);

        return new CallNode($"{name}.{method}", args);
    }

    private AstNode ParseCallOrInstruction()
    {
        // Could be "call AddTwo(10, 20)" or "mov(1, rax)"
        var identifier = Peek().Value;
        Advance();

        if (Peek().Type == TokenType.Dot)
        {
            // It's a dotted call like stdout.put(...)
            Advance();
            var method = Expect(TokenType.Identifier).Value;
            Expect(TokenType.LeftParen);
            var args = new List<AstNode>();
            if (Peek().Type != TokenType.RightParen)
            {
                args.Add(ParseOperand());
                while (Peek().Type == TokenType.Comma)
                {
                    Advance();
                    args.Add(ParseOperand());
                }
            }
            Expect(TokenType.RightParen);
            Expect(TokenType.Semicolon);
            return new CallNode($"{identifier}.{method}", args);
        }

        if (Peek().Type == TokenType.LeftParen)
        {
            Advance(); // skip (
            var operands = new List<AstNode>();
            if (Peek().Type != TokenType.RightParen)
            {
                operands.Add(ParseOperand());
                while (Peek().Type == TokenType.Comma)
                {
                    Advance();
                    operands.Add(ParseOperand());
                }
            }
            Expect(TokenType.RightParen);
            Expect(TokenType.Semicolon);
            return new InstructionNode(identifier, operands);
        }

        // Just an identifier with no parens — treat as comment or skip
        Expect(TokenType.Semicolon);
        return new InstructionNode(identifier, new List<AstNode>());
    }

    private AstNode ParseInstruction()
    {
        // Parse: mnemonic(operand1, operand2, ...) ;
        var mnemonic = Expect(TokenType.Identifier).Value;
        Expect(TokenType.LeftParen);
        var operands = new List<AstNode>();
        if (Peek().Type != TokenType.RightParen)
        {
            operands.Add(ParseOperand());
            while (Peek().Type == TokenType.Comma)
            {
                Advance();
                operands.Add(ParseOperand());
            }
        }
        Expect(TokenType.RightParen);
        Expect(TokenType.Semicolon);
        return new InstructionNode(mnemonic, operands);
    }

    private AstNode ParseOperand()
    {
        var token = Peek();

        if (token.Type == TokenType.Ampersand)
        {
            Advance();
            var nameToken = Peek();
            if (nameToken.Type == TokenType.StringLiteral)
            {
                var lit = Advance();
                return WithLocation(new AddressOfStringNode(lit.Value), lit);
            }

            string name;
            if (nameToken.Type == TokenType.Identifier)
            {
                name = Advance().Value;
            }
            else if (IsRegisterToken(nameToken.Type))
            {
                name = Advance().Value.ToLowerInvariant();
            }
            else
            {
                throw new ParseException(
                    $"Expected variable name or string literal after '&' but got '{nameToken.Type}' ('{nameToken.Value}') at line {nameToken.Line}, column {nameToken.Column}");
            }

            return WithLocation(new AddressOfNode(name), nameToken);
        }

        if (token.Type == TokenType.LeftBracket)
            return ParseMemoryRef();

        switch (token.Type)
        {
            case TokenType.Minus:
                if (_pos + 1 < _tokens.Count && _tokens[_pos + 1].Type == TokenType.IntegerLiteral)
                {
                    Advance(); // skip -
                    var lit = Advance();
                    return new IntegerLiteralNode(long.Parse("-" + lit.Value));
                }
                Advance();
                return new IdentifierNode("-");

            case TokenType.IntegerLiteral:
                Advance();
                return new IntegerLiteralNode(long.Parse(token.Value));

            case TokenType.StringLiteral:
                Advance();
                return new StringLiteralNode(token.Value);

            case TokenType.Identifier:
            {
                var name = Advance().Value;
                if (Peek().Type == TokenType.LeftBracket)
                {
                    Advance();
                    var index = ParseOperand();
                    var close = Expect(TokenType.RightBracket);
                    return WithLocation(new ArrayIndexNode(name, index), token);
                }

                return new IdentifierNode(name);
            }

            // Register tokens
            case TokenType.RAX: case TokenType.RBX: case TokenType.RCX: case TokenType.RDX:
            case TokenType.RSI: case TokenType.RDI: case TokenType.RBP: case TokenType.RSP:
            case TokenType.R8: case TokenType.R9: case TokenType.R10: case TokenType.R11:
            case TokenType.R12: case TokenType.R13: case TokenType.R14: case TokenType.R15:
            case TokenType.EAX: case TokenType.EBX: case TokenType.ECX: case TokenType.EDX:
            case TokenType.ESI: case TokenType.EDI: case TokenType.EBP: case TokenType.ESP:
            case TokenType.R8D: case TokenType.R9D: case TokenType.R10D: case TokenType.R11D:
            case TokenType.R12D: case TokenType.R13D: case TokenType.R14D: case TokenType.R15D:
            case TokenType.AX: case TokenType.BX: case TokenType.CX: case TokenType.DX:
            case TokenType.AL: case TokenType.BL: case TokenType.CL: case TokenType.DL:
                Advance();
                return new RegisterNode(token.Value.ToLowerInvariant());

            default:
                // Unexpected token, advance and return identifier
                Advance();
                return new IdentifierNode(token.Value);
        }
    }

    private MemoryRefNode ParseMemoryRef()
    {
        var open = Expect(TokenType.LeftBracket);
        var regToken = Peek();
        if (!IsRegisterToken(regToken.Type))
        {
            throw new ParseException(
                $"Memory reference requires a register but got '{regToken.Type}' ('{regToken.Value}') at line {regToken.Line}, column {regToken.Column}");
        }

        var register = Advance().Value.ToLowerInvariant();
        long offset = 0;
        if (Peek().Type == TokenType.Plus)
        {
            Advance();
            offset = ParseOffsetLiteral();
        }
        else if (Peek().Type == TokenType.Minus)
        {
            Advance();
            offset = -ParseOffsetLiteral();
        }

        Expect(TokenType.RightBracket);
        var sizeBits = 64;
        if (Peek().Type == TokenType.Dot)
        {
            Advance();
            sizeBits = ParseMemSizeQualifierFromToken();
        }

        return WithLocation(new MemoryRefNode(register, offset, sizeBits), open);
    }

    private long ParseOffsetLiteral()
    {
        var token = Peek();
        if (token.Type == TokenType.IntegerLiteral)
        {
            Advance();
            return long.Parse(token.Value);
        }

        throw new ParseException(
            $"Expected integer offset but got '{token.Type}' ('{token.Value}') at line {token.Line}, column {token.Column}");
    }

    private int ParseMemSizeQualifierFromToken()
    {
        var token = Expect(TokenType.Identifier);
        return token.Value.ToLowerInvariant() switch
        {
            "byte" => 8,
            "word" => 16,
            "dword" => 32,
            "qword" => 64,
            _ => throw new ParseException(
                $"Expected .byte, .word, .dword, or .qword after memory reference but got '.{token.Value}' at line {token.Line}, column {token.Column}")
        };
    }

    private static bool IsRegisterToken(TokenType type)
        => type is TokenType.RAX or TokenType.RBX or TokenType.RCX or TokenType.RDX
            or TokenType.RSI or TokenType.RDI or TokenType.RBP or TokenType.RSP
            or TokenType.R8 or TokenType.R9 or TokenType.R10 or TokenType.R11
            or TokenType.R12 or TokenType.R13 or TokenType.R14 or TokenType.R15
            or TokenType.EAX or TokenType.EBX or TokenType.ECX or TokenType.EDX
            or TokenType.ESI or TokenType.EDI or TokenType.EBP or TokenType.ESP
            or TokenType.R8D or TokenType.R9D or TokenType.R10D or TokenType.R11D
            or TokenType.R12D or TokenType.R13D or TokenType.R14D or TokenType.R15D
            or TokenType.AX or TokenType.BX or TokenType.CX or TokenType.DX
            or TokenType.AL or TokenType.BL or TokenType.CL or TokenType.DL;

    private Token Peek()
    {
        return _pos < _tokens.Count ? _tokens[_pos] : _tokens[^1];
    }

    private Token Advance()
    {
        var token = Peek();
        _pos++;
        return token;
    }

    private static T WithLocation<T>(T node, Token token) where T : AstNode
    {
        node.Line = token.Line;
        node.Column = token.Column;
        return node;
    }

    private Token Expect(TokenType expected)
    {
        var token = Peek();
        if (token.Type != expected)
        {
            throw new ParseException(
                $"Expected '{expected}' but got '{token.Type}' ('{token.Value}') at line {token.Line}, column {token.Column}");
        }
        return Advance();
    }
}

public sealed class ParseException : Exception
{
    public int Line { get; }
    public int Column { get; }

    public ParseException(string message) : base(message)
    {
        // Try to extract line/column from the message
        var parts = message.Split(" at line ");
        if (parts.Length == 2)
        {
            var posParts = parts[1].Split(", column ");
            if (posParts.Length == 2 && int.TryParse(posParts[0], out var line) && int.TryParse(posParts[1], out var col))
            {
                Line = line;
                Column = col;
            }
        }
    }
}