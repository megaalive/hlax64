namespace HlaX64.Compiler.Ir;

public enum IrOpcode
{
    LoadConstant,
    LoadLocal,
    StoreLocal,
    Move,
    Add,
    Subtract,
    Multiply,
    Divide,
    Modulo,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    BitwiseNot,
    ShiftLeft,
    ShiftRight,
    CompareToBool,
    Compare,
    Branch,
    ConditionalBranch,
    Call,
    Return
}

public enum CompareKind
{
    Equal,
    NotEqual,
    LessThanSigned,
    LessThanUnsigned,
    LessOrEqualSigned,
    LessOrEqualUnsigned,
    GreaterThanSigned,
    GreaterThanUnsigned,
    GreaterOrEqualSigned,
    GreaterOrEqualUnsigned
}

public sealed class IrInstruction
{
    public IrOpcode Opcode { get; }
    public IrValue? Destination { get; set; }
    public List<IrValue> Operands { get; }
    public object? Immediate { get; }

    public string? TargetBlock { get; set; }
    public CompareKind? CmpKind { get; set; }

    public IrInstruction(IrOpcode opcode, IrValue? destination = null, List<IrValue>? operands = null, object? immediate = null)
    {
        Opcode = opcode;
        Destination = destination;
        Operands = operands ?? new List<IrValue>();
        Immediate = immediate;
    }

    public override string ToString()
    {
        var dst = Destination != null ? $"{Destination} = " : "";
        var ops = Operands.Count > 0 ? string.Join(", ", Operands) : "";
        var imm = Immediate != null ? $" [{Immediate}]" : "";
        var block = TargetBlock != null ? $" -> {TargetBlock}" : "";
        var cmp = CmpKind != null ? $" ({CmpKind})" : "";
        return $"{dst}{Opcode}{cmp} {ops}{imm}{block}".Trim();
    }
}
