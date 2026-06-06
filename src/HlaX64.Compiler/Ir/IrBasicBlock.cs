namespace HlaX64.Compiler.Ir;

public sealed class IrBasicBlock
{
    private static int _nextId;

    public int Id { get; }
    public string Label { get; set; }
    public List<IrInstruction> Instructions { get; }

    public IrBasicBlock(string? label = null)
    {
        Id = _nextId++;
        Label = label ?? $"bb{Id}";
        Instructions = new List<IrInstruction>();
    }

    public void Add(IrInstruction inst) => Instructions.Add(inst);

    public override string ToString() => $"{Label}:";
}
