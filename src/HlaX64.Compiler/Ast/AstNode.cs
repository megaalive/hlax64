namespace HlaX64.Compiler.Ast;

/// <summary>
/// Base class for all AST nodes.
/// </summary>
public abstract class AstNode
{
    public abstract string Kind { get; }
    public int Line { get; set; }
    public int Column { get; set; }
}

/// <summary>
/// Represents the root program: program name; begin name; statements; end name;
/// </summary>
public class ProgramNode : AstNode
{
    public override string Kind => "Program";
    public string Name { get; }
    public List<AstNode> Includes { get; }
    public List<AstNode> Statements { get; }

    public ProgramNode(string name, List<AstNode> includes, List<AstNode> statements)
    {
        Name = name;
        Includes = includes;
        Statements = statements;
    }
}

/// <summary>
/// Represents an #include directive.
/// </summary>
public class IncludeNode : AstNode
{
    public override string Kind => "Include";
    public string Path { get; }

    public IncludeNode(string path)
    {
        Path = path;
    }
}

/// <summary>
/// Represents a block of statements (begin/end body).
/// </summary>
public class BlockNode : AstNode
{
    public override string Kind => "Block";
    public string Label { get; }
    public List<AstNode> Statements { get; }

    public BlockNode(string label, List<AstNode> statements)
    {
        Label = label;
        Statements = statements;
    }
}

/// <summary>
/// Represents an instruction like mov(src, dst), add(val, reg), etc.
/// </summary>
public class InstructionNode : AstNode
{
    public override string Kind => "Instruction";
    public string Mnemonic { get; }
    public List<AstNode> Operands { get; }

    public InstructionNode(string mnemonic, List<AstNode> operands)
    {
        Mnemonic = mnemonic;
        Operands = operands;
    }
}

/// <summary>
/// Represents a function/procedure call: stdout.put(...), call AddTwo(10, 20), etc.
/// </summary>
public class CallNode : AstNode
{
    public override string Kind => "Call";
    public string Name { get; }
    public List<AstNode> Arguments { get; }

    public CallNode(string name, List<AstNode> arguments)
    {
        Name = name;
        Arguments = arguments;
    }
}

/// <summary>
/// Represents a procedure declaration.
/// </summary>
public class ProcedureNode : AstNode
{
    public override string Kind => "Procedure";
    public string Name { get; }
    public List<ParameterNode> Parameters { get; }
    public string? ReturnsRegister { get; }
    public bool IsExport { get; }
    public List<AstNode> Variables { get; }
    public List<AstNode> Body { get; }

    public ProcedureNode(string name, List<ParameterNode> parameters, string? returnsRegister, bool isExport, List<AstNode> variables, List<AstNode> body)
    {
        Name = name;
        Parameters = parameters;
        ReturnsRegister = returnsRegister;
        IsExport = isExport;
        Variables = variables;
        Body = body;
    }
}

/// <summary>
/// Represents a procedure parameter.
/// </summary>
public class ParameterNode : AstNode
{
    public override string Kind => "Parameter";
    public string Name { get; }
    public string Type { get; }

    public ParameterNode(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

/// <summary>
/// Represents a variable declaration.
/// </summary>
public class VariableNode : AstNode
{
    public override string Kind => "Variable";
    public string Name { get; }
    public string Type { get; }

    public VariableNode(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

/// <summary>
/// Represents a register operand.
/// </summary>
public class RegisterNode : AstNode
{
    public override string Kind => "Register";
    public string Name { get; }

    public RegisterNode(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Represents an integer literal.
/// </summary>
public class IntegerLiteralNode : AstNode
{
    public override string Kind => "IntegerLiteral";
    public long Value { get; }

    public IntegerLiteralNode(long value)
    {
        Value = value;
    }
}

/// <summary>
/// Represents a string literal.
/// </summary>
public class StringLiteralNode : AstNode
{
    public override string Kind => "StringLiteral";
    public string Value { get; }

    public StringLiteralNode(string value)
    {
        Value = value;
    }
}

/// <summary>
/// Address-of a stack variable: &amp;name
/// </summary>
public class AddressOfNode : AstNode
{
    public override string Kind => "AddressOf";
    public string VariableName { get; }

    public AddressOfNode(string variableName)
    {
        VariableName = variableName;
    }
}

/// <summary>
/// Memory dereference: [registerOrPtr]
/// </summary>
public class MemoryRefNode : AstNode
{
    public override string Kind => "MemoryRef";
    public AstNode Inner { get; }

    public MemoryRefNode(AstNode inner)
    {
        Inner = inner;
    }
}

/// <summary>
/// Represents an identifier (variable reference, label, etc.).
/// </summary>
public class IdentifierNode : AstNode
{
    public override string Kind => "Identifier";
    public string Name { get; }

    public IdentifierNode(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Represents an if/then/else/endif control flow.
/// </summary>
public class IfNode : AstNode
{
    public override string Kind => "If";
    public AstNode Condition { get; }
    public List<AstNode> ThenBody { get; }
    public List<AstNode> ElseBody { get; }

    public IfNode(AstNode condition, List<AstNode> thenBody, List<AstNode> elseBody)
    {
        Condition = condition;
        ThenBody = thenBody;
        ElseBody = elseBody;
    }
}

/// <summary>
/// Represents a comparison condition (e.g., rax = 0, rcx < rdx).
/// </summary>
public class ComparisonNode : AstNode
{
    public override string Kind => "Comparison";
    public AstNode Left { get; }
    public string Operator { get; } // "=", "<", ">"
    public AstNode Right { get; }

    public ComparisonNode(AstNode left, string op, AstNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
    }
}

/// <summary>
/// Represents a while/do/endwhile loop.
/// </summary>
public class WhileNode : AstNode
{
    public override string Kind => "While";
    public AstNode Condition { get; }
    public List<AstNode> Body { get; }

    public WhileNode(AstNode condition, List<AstNode> body)
    {
        Condition = condition;
        Body = body;
    }
}

/// <summary>
/// Represents a pragma directive.
/// </summary>
public class PragmaNode : AstNode
{
    public override string Kind => "Pragma";
    public string Directive { get; }
    public string Value { get; }

    public PragmaNode(string directive, string value)
    {
        Directive = directive;
        Value = value;
    }
}