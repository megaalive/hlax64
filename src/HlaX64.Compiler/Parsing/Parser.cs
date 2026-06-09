using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;

namespace HlaX64.Compiler.Parsing;

public sealed class Parser
{
    private readonly List<Token> _tokens;
    private int _pos;
    private readonly List<Diagnostic> _diagnostics = [];

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Exists(d => d.Severity == DiagnosticSeverity.Error);

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

        // Optional program-level declarations before begin (any order)
        var constants = new List<AstNode>();
        var enums = new List<AstNode>();
        var records = new List<AstNode>();
        var statics = new List<AstNode>();
        var externs = new List<AstNode>();
        var typeAliases = new List<AstNode>();
        var procedures = new List<AstNode>();
        while (Peek().Type is TokenType.Const or TokenType.Enum or TokenType.Record or TokenType.Static
            or TokenType.Procedure or TokenType.Export or TokenType.Extern or TokenType.Type)
        {
            switch (Peek().Type)
            {
                case TokenType.Const:
                    constants.Add(ParseConstBlock());
                    break;
                case TokenType.Enum:
                    enums.Add(ParseEnumBlock());
                    break;
                case TokenType.Record:
                    records.Add(ParseRecordBlock());
                    break;
                case TokenType.Static:
                    statics.Add(ParseStaticBlock());
                    break;
                case TokenType.Extern:
                    externs.Add(ParseExternProcedure());
                    break;
                case TokenType.Type:
                    typeAliases.Add(ParseTypeAlias());
                    break;
                default:
                    procedures.Add(ParseProcedure());
                    break;
            }
        }

        // begin name;
        Expect(TokenType.Begin);
        var beginToken = Expect(TokenType.Identifier);
        var beginName = beginToken.Value;
        Expect(TokenType.Semicolon);

        // Statements
        var statements = new List<AstNode>();
        while (Peek().Type != TokenType.End && Peek().Type != TokenType.EndOfFile)
        {
            statements.AddRange(ParseStatementsUntilEnd());
        }

        // end name;
        Expect(TokenType.End);
        var endToken = Expect(TokenType.Identifier);
        var endName = endToken.Value;
        Expect(TokenType.Semicolon);

        // Combine procedures + main statements into the program
        var allStatements = new List<AstNode>();
        allStatements.AddRange(procedures);
        allStatements.AddRange(statements);

        return new ProgramNode(programName, beginName, endName,
            beginToken.Line, beginToken.Column, endToken.Line, endToken.Column,
            includes, constants, enums, records, statics, externs, typeAliases, allStatements);
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

            var startPos = _pos;
            var startDiagCount = _diagnostics.Count;
            try
            {
                stmts.Add(ParseStatement());
            }
            catch (ParseException ex)
            {
                ReportParseException(ex);
                _pos = startPos;
                while (_diagnostics.Count > startDiagCount + 1)
                    _diagnostics.RemoveAt(_diagnostics.Count - 1);
                SyncToNextStatement();
            }
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

        if (token.Type == TokenType.Break)
        {
            Advance();
            Expect(TokenType.Semicolon);
            return WithLocation(new BreakNode(), token);
        }

        if (token.Type == TokenType.Continue)
        {
            Advance();
            Expect(TokenType.Semicolon);
            return WithLocation(new ContinueNode(), token);
        }

        if (token.Type == TokenType.Identifier && _pos + 1 < _tokens.Count &&
            _tokens[_pos + 1].Type == TokenType.Colon)
        {
            var name = Advance().Value;
            Advance();
            return WithLocation(new LabelNode(name), token);
        }

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

        // Runtime assignment: ident := expr; or rax := expr;
        if (TryParseAssignTarget(out var target))
        {
            Expect(TokenType.ColonAssign);
            var expr = ParseRuntimeExpression();
            Expect(TokenType.Semicolon);
            return WithLocation(new AssignExprNode(target, expr), token);
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

        var parameters = ParseParameterList();
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

        // Optional const / enum / record declarations
        var constants = new List<AstNode>();
        var enums = new List<AstNode>();
        var records = new List<AstNode>();
        while (Peek().Type is TokenType.Const or TokenType.Enum or TokenType.Record)
        {
            switch (Peek().Type)
            {
                case TokenType.Const:
                    constants.Add(ParseConstBlock());
                    break;
                case TokenType.Enum:
                    enums.Add(ParseEnumBlock());
                    break;
                case TokenType.Record:
                    records.Add(ParseRecordBlock());
                    break;
            }
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
                AstNode? arraySize = null;
                if (Peek().Type == TokenType.LeftBracket)
                {
                    Advance();
                    arraySize = ParseConstExpression();
                    Expect(TokenType.RightBracket);
                }

                Expect(TokenType.Semicolon);
                variables.Add(WithLocation(new VariableNode(varToken.Value, typeToken.Value, arraySize), varToken));
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

        return new ProcedureNode(name, parameters, returnsRegister, isExport,
            isExtern: false, returnType: null, linkLibrary: null, isVariadic: false,
            constants, enums, records, variables, body);
    }

    private ExternProcedureNode ParseExternProcedure()
    {
        var open = Expect(TokenType.Extern);
        var isVariadic = false;
        if (Peek().Type == TokenType.Variadic)
        {
            isVariadic = true;
            Advance();
        }

        Expect(TokenType.Procedure);
        var name = Expect(TokenType.Identifier).Value;
        var parameters = ParseParameterList();
        var returnType = ParseReturnTypeClause(required: true);
        var linkLibrary = ParseFromClause();
        ConsumeSemicolon("extern procedure declaration");
        return WithLocation(new ExternProcedureNode(name, parameters, returnType, linkLibrary, isVariadic), open);
    }

    private TypeAliasNode ParseTypeAlias()
    {
        var open = Expect(TokenType.Type);
        var name = Expect(TokenType.Identifier).Value;
        Expect(TokenType.ColonAssign);
        Expect(TokenType.Procedure);
        var parameters = ParseParameterList();
        var returnType = ParseReturnTypeClause(required: true);
        ConsumeSemicolon("type alias declaration");
        return WithLocation(new TypeAliasNode(name, parameters, returnType), open);
    }

    private List<ParameterNode> ParseParameterList()
    {
        var parameters = new List<ParameterNode>();
        if (Peek().Type != TokenType.LeftParen)
            return parameters;

        Advance();
        while (Peek().Type != TokenType.RightParen && Peek().Type != TokenType.EndOfFile)
        {
            if (Peek().Type == TokenType.Semicolon)
            {
                Advance();
                continue;
            }

            if (TryParseParameter(out var param))
            {
                parameters.Add(param);
                if (Peek().Type == TokenType.Identifier)
                {
                    var next = Peek();
                    ReportParseError(
                        $"Expected ';' between procedure parameters but got '{next.Type}' ('{next.Value}')",
                        next.Line, next.Column);
                }
            }
            else
            {
                RecoverInParameterList();
            }
        }

        if (Peek().Type == TokenType.RightParen)
        {
            Advance();
        }
        else
        {
            var token = Peek();
            ReportParseError(
                $"Expected ')' to close parameter list but got '{token.Type}' ('{token.Value}')",
                token.Line, token.Column);
            SkipUntilRightParen();
        }

        return parameters;
    }

    private string ParseReturnTypeClause(bool required)
    {
        if (Peek().Type == TokenType.Colon)
        {
            Advance();
            if (Peek().Type == TokenType.Identifier)
                return Advance().Value;

            var token = Peek();
            ReportParseError(
                $"Expected return type identifier but got '{token.Type}' ('{token.Value}')",
                token.Line, token.Column);
            return "void";
        }

        if (required)
        {
            var token = Peek();
            ReportParseError(
                $"Expected return type after ')' but got '{token.Type}' ('{token.Value}')",
                token.Line, token.Column);
        }

        return "void";
    }

    private string? ParseFromClause()
    {
        if (Peek().Type != TokenType.From)
            return null;

        Advance();
        if (Peek().Type == TokenType.StringLiteral)
            return Advance().Value;

        var token = Peek();
        ReportParseError(
            $"Expected library name string after 'from' but got '{token.Type}' ('{token.Value}')",
            token.Line, token.Column);
        return null;
    }

    private ConstBlockNode ParseConstBlock()
    {
        var open = Expect(TokenType.Const);
        var declarations = new List<ConstDeclarationNode>();

        while (Peek().Type == TokenType.Identifier)
        {
            var nameToken = Expect(TokenType.Identifier);
            Expect(TokenType.ColonAssign);
            var expr = ParseConstExpression();
            Expect(TokenType.Semicolon);
            declarations.Add(WithLocation(new ConstDeclarationNode(nameToken.Value, expr), nameToken));
        }

        Expect(TokenType.Endconst);
        Expect(TokenType.Semicolon);
        return WithLocation(new ConstBlockNode(declarations), open);
    }

    private EnumBlockNode ParseEnumBlock()
    {
        var open = Expect(TokenType.Enum);
        var nameToken = Expect(TokenType.Identifier);
        Expect(TokenType.Colon);
        var backingToken = Expect(TokenType.Identifier);
        var members = new List<EnumMemberNode>();

        while (Peek().Type == TokenType.Identifier)
        {
            var memberToken = Expect(TokenType.Identifier);
            AstNode? value = null;
            if (Peek().Type == TokenType.ColonAssign)
            {
                Advance();
                value = ParseConstExpression();
            }
            Expect(TokenType.Semicolon);
            members.Add(WithLocation(new EnumMemberNode(memberToken.Value, value), memberToken));
        }

        Expect(TokenType.Endenum);
        Expect(TokenType.Semicolon);
        return WithLocation(new EnumBlockNode(nameToken.Value, backingToken.Value, members), open);
    }

    private RecordBlockNode ParseRecordBlock()
    {
        var open = Expect(TokenType.Record);
        var nameToken = Expect(TokenType.Identifier);
        var isPacked = false;
        if (Peek().Type == TokenType.Packed)
        {
            Advance();
            isPacked = true;
        }

        var fields = new List<RecordFieldNode>();

        while (Peek().Type == TokenType.Identifier)
        {
            var fieldToken = Expect(TokenType.Identifier);
            Expect(TokenType.Colon);
            var typeToken = Expect(TokenType.Identifier);
            Expect(TokenType.Semicolon);
            fields.Add(WithLocation(new RecordFieldNode(fieldToken.Value, typeToken.Value), fieldToken));
        }

        Expect(TokenType.Endrecord);
        Expect(TokenType.Semicolon);
        return WithLocation(new RecordBlockNode(nameToken.Value, fields, isPacked), open);
    }

    private StaticBlockNode ParseStaticBlock()
    {
        var open = Expect(TokenType.Static);
        var declarations = new List<StaticDeclarationNode>();

        while (Peek().Type == TokenType.Identifier)
        {
            var nameToken = Expect(TokenType.Identifier);
            Expect(TokenType.Colon);
            var typeToken = Expect(TokenType.Identifier);
            AstNode? arraySize = null;
            if (Peek().Type == TokenType.LeftBracket)
            {
                Advance();
                arraySize = ParseConstExpression();
                Expect(TokenType.RightBracket);
            }

            AstNode? initializer = null;
            if (Peek().Type == TokenType.ColonAssign)
            {
                Advance();
                initializer = ParseConstExpression();
            }

            Expect(TokenType.Semicolon);
            declarations.Add(WithLocation(
                new StaticDeclarationNode(nameToken.Value, typeToken.Value, arraySize, initializer), nameToken));
        }

        Expect(TokenType.Endstatic);
        Expect(TokenType.Semicolon);
        return WithLocation(new StaticBlockNode(declarations), open);
    }

    private AstNode ParseConstExpression()
    {
        return ParseBitwiseOr();
    }

    private AstNode ParseBitwiseOr()
    {
        var left = ParseBitwiseXor();
        while (Peek().Type == TokenType.Pipe)
        {
            var op = Advance();
            var right = ParseBitwiseXor();
            left = WithLocation(new BinaryExprNode(left, "|", right), op);
        }
        return left;
    }

    private AstNode ParseBitwiseXor()
    {
        var left = ParseBitwiseAnd();
        while (Peek().Type == TokenType.Caret)
        {
            var op = Advance();
            var right = ParseBitwiseAnd();
            left = WithLocation(new BinaryExprNode(left, "^", right), op);
        }
        return left;
    }

    private AstNode ParseBitwiseAnd()
    {
        var left = ParseShift();
        while (Peek().Type == TokenType.Ampersand)
        {
            var op = Advance();
            var right = ParseShift();
            left = WithLocation(new BinaryExprNode(left, "&", right), op);
        }
        return left;
    }

    private AstNode ParseShift()
    {
        var left = ParseAdditive();
        while (Peek().Type is TokenType.ShiftLeft or TokenType.ShiftRight)
        {
            var op = Advance();
            var right = ParseAdditive();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseAdditive()
    {
        var left = ParseMultiplicative();
        while (Peek().Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Advance();
            var right = ParseMultiplicative();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseMultiplicative()
    {
        var left = ParseUnary();
        while (Peek().Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            var op = Advance();
            var right = ParseUnary();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseUnary()
    {
        var token = Peek();
        if (token.Type is TokenType.Minus or TokenType.Tilde)
        {
            Advance();
            var operand = ParseUnary();
            return WithLocation(new UnaryExprNode(token.Value, operand), token);
        }

        return ParseConstPrimary();
    }

    private AstNode ParseConstPrimary()
    {
        var token = Peek();

        if (token.Type == TokenType.LeftParen)
        {
            Advance();
            var expr = ParseConstExpression();
            Expect(TokenType.RightParen);
            return expr;
        }

        if (token.Type == TokenType.IntegerLiteral)
        {
            Advance();
            return new IntegerLiteralNode(long.Parse(token.Value));
        }

        if (token.Type == TokenType.Identifier)
        {
            return ParseConstIdentifierOrBuiltin(token);
        }

        throw new ParseException(
            $"Expected integer literal, constant name, or '(' in compile-time expression but got '{token.Type}' ('{token.Value}') at line {token.Line}, column {token.Column}");
    }

    private AstNode ParseConstIdentifierOrBuiltin(Token token)
    {
        var name = Advance().Value;
        if (string.Equals(name, "sizeof", StringComparison.OrdinalIgnoreCase)
            && Peek().Type == TokenType.LeftParen)
        {
            Advance();
            var typeName = Expect(TokenType.Identifier).Value;
            Expect(TokenType.RightParen);
            return WithLocation(new SizeofNode(typeName), token);
        }

        if (string.Equals(name, "offsetof", StringComparison.OrdinalIgnoreCase)
            && Peek().Type == TokenType.LeftParen)
        {
            Advance();
            var typeName = Expect(TokenType.Identifier).Value;
            Expect(TokenType.Comma);
            var fieldName = Expect(TokenType.Identifier).Value;
            Expect(TokenType.RightParen);
            return WithLocation(new OffsetofNode(typeName, fieldName), token);
        }

        if (Peek().Type == TokenType.Dot)
        {
            Advance();
            var memberToken = Expect(TokenType.Identifier);
            return WithLocation(new DotAccessNode(name, memberToken.Value), token);
        }

        return new IdentifierNode(name);
    }

    private bool TryParseAssignTarget(out AstNode target)
    {
        target = null!;
        var token = Peek();
        if (token.Type == TokenType.Identifier &&
            _pos + 1 < _tokens.Count &&
            _tokens[_pos + 1].Type == TokenType.ColonAssign)
        {
            var name = Advance().Value;
            target = WithLocation(new IdentifierNode(name), token);
            return true;
        }

        if (IsRegisterToken(token.Type) &&
            _pos + 1 < _tokens.Count &&
            _tokens[_pos + 1].Type == TokenType.ColonAssign)
        {
            var reg = Advance().Value.ToLowerInvariant();
            target = WithLocation(new RegisterNode(reg), token);
            return true;
        }

        return false;
    }

    private AstNode ParseRuntimeExpression()
    {
        return ParseRuntimeComparison();
    }

    private AstNode ParseRuntimeComparison()
    {
        var left = ParseRuntimeBitwiseOr();
        while (Peek().Type is TokenType.DoubleEquals or TokenType.NotEquals or TokenType.LessThan
            or TokenType.GreaterThan or TokenType.LessOrEqual or TokenType.GreaterOrEqual)
        {
            var op = Advance();
            var right = ParseRuntimeBitwiseOr();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeBitwiseOr()
    {
        var left = ParseRuntimeBitwiseXor();
        while (Peek().Type == TokenType.Pipe)
        {
            var op = Advance();
            var right = ParseRuntimeBitwiseXor();
            left = WithLocation(new BinaryExprNode(left, "|", right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeBitwiseXor()
    {
        var left = ParseRuntimeBitwiseAnd();
        while (Peek().Type == TokenType.Caret)
        {
            var op = Advance();
            var right = ParseRuntimeBitwiseAnd();
            left = WithLocation(new BinaryExprNode(left, "^", right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeBitwiseAnd()
    {
        var left = ParseRuntimeShift();
        while (Peek().Type == TokenType.Ampersand)
        {
            var op = Advance();
            var right = ParseRuntimeShift();
            left = WithLocation(new BinaryExprNode(left, "&", right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeShift()
    {
        var left = ParseRuntimeAdditive();
        while (Peek().Type is TokenType.ShiftLeft or TokenType.ShiftRight)
        {
            var op = Advance();
            var right = ParseRuntimeAdditive();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeAdditive()
    {
        var left = ParseRuntimeMultiplicative();
        while (Peek().Type is TokenType.Plus or TokenType.Minus)
        {
            var op = Advance();
            var right = ParseRuntimeMultiplicative();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeMultiplicative()
    {
        var left = ParseRuntimeUnary();
        while (Peek().Type is TokenType.Star or TokenType.Slash or TokenType.Percent)
        {
            var op = Advance();
            var right = ParseRuntimeUnary();
            left = WithLocation(new BinaryExprNode(left, op.Value, right), op);
        }
        return left;
    }

    private AstNode ParseRuntimeUnary()
    {
        var token = Peek();
        if (token.Type is TokenType.Minus or TokenType.Tilde)
        {
            Advance();
            var operand = ParseRuntimeUnary();
            return WithLocation(new UnaryExprNode(token.Value, operand), token);
        }

        return ParseRuntimePrimary();
    }

    private AstNode ParseRuntimePrimary()
    {
        var token = Peek();

        if (token.Type == TokenType.LeftParen)
        {
            Advance();
            var expr = ParseRuntimeExpression();
            Expect(TokenType.RightParen);
            return expr;
        }

        if (token.Type == TokenType.IntegerLiteral)
        {
            Advance();
            return new IntegerLiteralNode(long.Parse(token.Value));
        }

        if (token.Type == TokenType.Identifier)
        {
            Advance();
            return new IdentifierNode(token.Value);
        }

        if (IsRegisterToken(token.Type))
        {
            Advance();
            return new RegisterNode(token.Value.ToLowerInvariant());
        }

        throw new ParseException(
            $"Expected integer literal, name, register, or '(' in runtime expression but got '{token.Type}' ('{token.Value}') at line {token.Line}, column {token.Column}");
    }

    private ParameterNode ParseParameter()
        => TryParseParameter(out var param) ? param : new ParameterNode("_", "void");

    private bool TryParseParameter(out ParameterNode param)
    {
        param = null!;
        if (Peek().Type != TokenType.Identifier)
            return false;

        var nameToken = Advance();
        if (Peek().Type != TokenType.Colon)
        {
            ReportParseError(
                $"Expected ':' after parameter name '{nameToken.Value}'",
                nameToken.Line, nameToken.Column);
            SkipUntilSemicolonOrRightParen();
            return false;
        }

        Advance();
        if (Peek().Type != TokenType.Identifier)
        {
            var token = Peek();
            ReportParseError(
                $"Expected parameter type identifier but got '{token.Type}' ('{token.Value}')",
                token.Line, token.Column);
            SkipUntilSemicolonOrRightParen();
            return false;
        }

        var typeToken = Advance();
        while (Peek().Type == TokenType.Identifier)
        {
            var junk = Advance();
            ReportParseError(
                $"Unexpected token '{junk.Value}' after parameter type '{typeToken.Value}'",
                junk.Line, junk.Column);
        }

        param = new ParameterNode(nameToken.Value, typeToken.Value);
        return true;
    }

    private void RecoverInParameterList()
    {
        var token = Peek();
        ReportParseError(
            $"Expected parameter name but got '{token.Type}' ('{token.Value}')",
            token.Line, token.Column);
        Advance();
        SkipUntilSemicolonOrRightParen();
    }

    private void ConsumeSemicolon(string context)
    {
        if (Peek().Type == TokenType.Semicolon)
        {
            Advance();
            return;
        }

        var token = Peek();
        ReportParseError(
            $"Expected ';' after {context} but got '{token.Type}' ('{token.Value}')",
            token.Line, token.Column);
        SkipUntilSemicolon();
        if (Peek().Type == TokenType.Semicolon)
            Advance();
    }

    private void SyncToNextStatement()
    {
        if (_pos >= _tokens.Count)
            return;

        var startLine = Peek().Line;
        while (_pos < _tokens.Count)
        {
            var token = Peek();
            if (token.Type == TokenType.Semicolon)
            {
                Advance();
                return;
            }

            if (token.Line != startLine)
                return;

            Advance();
        }
    }

    private void SkipUntilSemicolon()
    {
        while (_pos < _tokens.Count && Peek().Type != TokenType.Semicolon)
            Advance();
    }

    private void SkipUntilSemicolonOrRightParen()
    {
        while (_pos < _tokens.Count)
        {
            var type = Peek().Type;
            if (type is TokenType.Semicolon or TokenType.RightParen)
                return;
            Advance();
        }
    }

    private void SkipUntilRightParen()
    {
        while (_pos < _tokens.Count && Peek().Type != TokenType.RightParen)
            Advance();
        if (Peek().Type == TokenType.RightParen)
            Advance();
    }

    private void ReportParseError(string message, int line, int column)
    {
        _diagnostics.Add(new Diagnostic("HLAX1000", DiagnosticSeverity.Error, message, line, column));
    }

    private void ReportParseException(ParseException ex)
    {
        var line = ex.Line > 0 ? ex.Line : 1;
        var column = ex.Column > 0 ? ex.Column : 1;
        ReportParseError(ex.Message, line, column);
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

        if (op.Type == TokenType.Equals || op.Type == TokenType.DoubleEquals ||
            op.Type == TokenType.NotEquals || op.Type == TokenType.LessThan ||
            op.Type == TokenType.GreaterThan || op.Type == TokenType.LessOrEqual ||
            op.Type == TokenType.GreaterOrEqual ||
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
        var mnemonicToken = Peek();
        var identifier = mnemonicToken.Value;
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
            return new InstructionNode(identifier, operands)
            {
                Line = mnemonicToken.Line,
                Column = mnemonicToken.Column
            };
        }

        // Just an identifier with no parens — treat as comment or skip
        Expect(TokenType.Semicolon);
        return new InstructionNode(identifier, new List<AstNode>())
        {
            Line = mnemonicToken.Line,
            Column = mnemonicToken.Column
        };
    }

    private AstNode ParseInstruction()
    {
        // Parse: mnemonic(operand1, operand2, ...) ;
        var mnemonicToken = Expect(TokenType.Identifier);
        var mnemonic = mnemonicToken.Value;
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
        return new InstructionNode(mnemonic, operands)
        {
            Line = mnemonicToken.Line,
            Column = mnemonicToken.Column
        };
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
                    Expect(TokenType.RightBracket);
                    return WithLocation(new ArrayIndexNode(name, index), token);
                }

                if (Peek().Type == TokenType.Dot)
                {
                    Advance();
                    var memberToken = Expect(TokenType.Identifier);
                    return WithLocation(new DotAccessNode(name, memberToken.Value), token);
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