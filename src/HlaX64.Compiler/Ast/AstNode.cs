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
    public List<AstNode> Constants { get; }
    public List<AstNode> Enums { get; }
    public List<AstNode> Records { get; }
    public List<AstNode> Statements { get; }

    public ProgramNode(string name, List<AstNode> includes, List<AstNode> constants,
        List<AstNode> enums, List<AstNode> records, List<AstNode> statements)
    {
        Name = name;
        Includes = includes;
        Constants = constants;
        Enums = enums;
        Records = records;
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
/// Runtime assignment: target := expression; (int64 scalar local or register).
/// </summary>
public class AssignExprNode : AstNode
{
    public override string Kind => "AssignExpr";
    public AstNode Target { get; }
    public AstNode Expression { get; }

    public AssignExprNode(AstNode target, AstNode expression)
    {
        Target = target;
        Expression = expression;
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
    public List<AstNode> Constants { get; }
    public List<AstNode> Variables { get; }
    public List<AstNode> Body { get; }
    /// <summary>Program + procedure compile-time constants after semantic analysis.</summary>
    public Dictionary<string, long> ResolvedConstants { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public ProcedureNode(string name, List<ParameterNode> parameters, string? returnsRegister, bool isExport,
        List<AstNode> constants, List<AstNode> variables, List<AstNode> body)
    {
        Name = name;
        Parameters = parameters;
        ReturnsRegister = returnsRegister;
        IsExport = isExport;
        Constants = constants;
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
    /// <summary>1 for scalar; N for array type[count]. Set by semantic after evaluating <see cref="ArraySizeExpression"/>.</summary>
    public int ElementCount { get; set; } = 1;
    /// <summary>Compile-time size expression when declared as type[N].</summary>
    public AstNode? ArraySizeExpression { get; }

    public VariableNode(string name, string type, AstNode? arraySizeExpression = null)
    {
        Name = name;
        Type = type;
        ArraySizeExpression = arraySizeExpression;
        if (arraySizeExpression is IntegerLiteralNode lit)
            ElementCount = checked((int)lit.Value);
    }
}

/// <summary>
/// enum Name: backingType ... endenum block.
/// </summary>
public class EnumBlockNode : AstNode
{
    public override string Kind => "EnumBlock";
    public string Name { get; }
    public string BackingType { get; }
    public List<EnumMemberNode> Members { get; }

    public EnumBlockNode(string name, string backingType, List<EnumMemberNode> members)
    {
        Name = name;
        BackingType = backingType;
        Members = members;
    }
}

/// <summary>
/// Single enum member: Name := compile-time value;
/// </summary>
public class EnumMemberNode : AstNode
{
    public override string Kind => "EnumMember";
    public string Name { get; }
    public AstNode Value { get; }

    public EnumMemberNode(string name, AstNode value)
    {
        Name = name;
        Value = value;
    }
}

/// <summary>
/// record Name ... endrecord type declaration.
/// </summary>
public class RecordBlockNode : AstNode
{
    public override string Kind => "RecordBlock";
    public string Name { get; }
    public List<RecordFieldNode> Fields { get; }

    public RecordBlockNode(string name, List<RecordFieldNode> fields)
    {
        Name = name;
        Fields = fields;
    }
}

/// <summary>
/// Record field: name: type;
/// </summary>
public class RecordFieldNode : AstNode
{
    public override string Kind => "RecordField";
    public string Name { get; }
    public string Type { get; }

    public RecordFieldNode(string name, string type)
    {
        Name = name;
        Type = type;
    }
}

/// <summary>
/// Qualified dot access: Enum.Member or var.field
/// </summary>
public class DotAccessNode : AstNode
{
    public override string Kind => "DotAccess";
    public string BaseName { get; }
    public string MemberName { get; }

    public DotAccessNode(string baseName, string memberName)
    {
        BaseName = baseName;
        MemberName = memberName;
    }
}

/// <summary>
/// Compile-time sizeof(RecordType)
/// </summary>
public class SizeofNode : AstNode
{
    public override string Kind => "Sizeof";
    public string TypeName { get; }

    public SizeofNode(string typeName) => TypeName = typeName;
}

/// <summary>
/// Compile-time offsetof(RecordType, field)
/// </summary>
public class OffsetofNode : AstNode
{
    public override string Kind => "Offsetof";
    public string TypeName { get; }
    public string FieldName { get; }

    public OffsetofNode(string typeName, string fieldName)
    {
        TypeName = typeName;
        FieldName = fieldName;
    }
}

/// <summary>
/// const ... endconst block with one or more name := expr declarations.
/// </summary>
public class ConstBlockNode : AstNode
{
    public override string Kind => "ConstBlock";
    public List<ConstDeclarationNode> Declarations { get; }

    public ConstBlockNode(List<ConstDeclarationNode> declarations)
    {
        Declarations = declarations;
    }
}

/// <summary>
/// Single compile-time constant: Name := Expression;
/// </summary>
public class ConstDeclarationNode : AstNode
{
    public override string Kind => "ConstDeclaration";
    public string Name { get; }
    public AstNode Expression { get; }

    public ConstDeclarationNode(string name, AstNode expression)
    {
        Name = name;
        Expression = expression;
    }
}

/// <summary>
/// Unary compile-time expression: -x, ~x
/// </summary>
public class UnaryExprNode : AstNode
{
    public override string Kind => "UnaryExpr";
    public string Operator { get; }
    public AstNode Operand { get; }

    public UnaryExprNode(string op, AstNode operand)
    {
        Operator = op;
        Operand = operand;
    }
}

/// <summary>
/// Binary compile-time expression with formal precedence.
/// </summary>
public class BinaryExprNode : AstNode
{
    public override string Kind => "BinaryExpr";
    public AstNode Left { get; }
    public string Operator { get; }
    public AstNode Right { get; }

    public BinaryExprNode(AstNode left, string op, AstNode right)
    {
        Left = left;
        Operator = op;
        Right = right;
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
/// Memory dereference: [reg], [reg + offset], optional .byte/.word/.dword/.qword
/// </summary>
public class MemoryRefNode : AstNode
{
    public override string Kind => "MemoryRef";
    public string Register { get; }
    public long Offset { get; }
    public int SizeBits { get; }

    public MemoryRefNode(string register, long offset = 0, int sizeBits = 64)
    {
        Register = register;
        Offset = offset;
        SizeBits = sizeBits;
    }
}

/// <summary>
/// Address-of a string literal in .rodata: &amp;"text"
/// </summary>
public class AddressOfStringNode : AstNode
{
    public override string Kind => "AddressOfString";
    public string Value { get; }

    public AddressOfStringNode(string value)
    {
        Value = value;
    }
}

/// <summary>
/// Indexed array element: arr[index]
/// </summary>
public class ArrayIndexNode : AstNode
{
    public override string Kind => "ArrayIndex";
    public string ArrayName { get; }
    public AstNode Index { get; }

    public ArrayIndexNode(string arrayName, AstNode index)
    {
        ArrayName = arrayName;
        Index = index;
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