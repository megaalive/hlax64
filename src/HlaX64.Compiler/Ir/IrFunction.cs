namespace HlaX64.Compiler.Ir;

public sealed class IrFunction
{
    public string Name { get; }
    public List<IrBasicBlock> Blocks { get; }
    public IrBasicBlock EntryBlock { get; }

    public IrFunction(string name)
    {
        Name = name;
        EntryBlock = new IrBasicBlock("entry");
        Blocks = new List<IrBasicBlock> { EntryBlock };
    }

    public void AddBlock(IrBasicBlock block) => Blocks.Add(block);

    public override string ToString()
    {
        var lines = new List<string> { $"function {Name}:" };
        foreach (var block in Blocks)
        {
            lines.Add($"  {block.Label}:");
            foreach (var inst in block.Instructions)
                lines.Add($"    {inst}");
        }
        return string.Join("\n", lines);
    }
}
