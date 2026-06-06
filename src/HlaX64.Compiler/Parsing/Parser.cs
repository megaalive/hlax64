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
        Expect(TokenType.LeftParen);

        var parameters = new List<ParameterNode>();
        if (Peek().Type != TokenType.RightParen)
        {
            parameters.Add(ParseParameter());
            while (Peek().Type == TokenType.Semicolon)
            {
                Advance(); // skip semicolon
                parameters.Add(ParseParameter());
            }
        }
        Expect(TokenType.RightParen);
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
                Expect(TokenType.Semicolon);
                variables.Add(new VariableNode(varToken.Value, typeToken.Value));
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

        if (op.Type == TokenType.Equals || op.Type == TokenType.LessThan || op.Type == TokenType.GreaterThan)
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

        switch (token.Type)
        {
            case TokenType.IntegerLiteral:
                Advance();
                return new IntegerLiteralNode(long.Parse(token.Value));

            case TokenType.StringLiteral:
                Advance();
                return new StringLiteralNode(token.Value);

            case TokenType.Identifier:
                Advance();
                return new IdentifierNode(token.Value);

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