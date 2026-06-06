namespace HlaX64.Compiler.Ir;

public sealed class IrFunction
{
    public string Name { get; }
    public List<IrBasicBlock> Blocks { get; }
    public IrBasicBlock EntryBlock { get; }
    public List<IrValue> ParameterValues { get; }
    public List<IrValue> LocalValues { get; }
    public Dictionary<string, IrLocalLayout> LocalLayouts { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsEntryPoint => Name == "_start";
    public bool IsExport { get; set; }

    public IrFunction(string name)
    {
        Name = name;
        EntryBlock = new IrBasicBlock("entry");
        Blocks = new List<IrBasicBlock> { EntryBlock };
        ParameterValues = new List<IrValue>();
        LocalValues = new List<IrValue>();
    }

    public void AddBlock(IrBasicBlock block)
    {
        if (!Blocks.Contains(block))
            Blocks.Add(block);
    }

    public void EnsureBlocksRegistered()
    {
        var toAdd = new List<IrBasicBlock>();
        void Walk(IrBasicBlock b)
        {
            if (Blocks.Contains(b)) return;
            toAdd.Add(b);
            foreach (var inst in b.Instructions)
            {
                if (inst.TargetBlock != null)
                {
                    var target = Blocks.FirstOrDefault(x => x.Label == inst.TargetBlock);
                    if (target != null && !toAdd.Contains(target))
                        Walk(target);
                }
            }
        }
        // Walk from entry and find all blocks referenced via branches
        foreach (var b in Blocks.ToList())
            Walk(b);
        foreach (var b in toAdd)
            if (!Blocks.Contains(b))
                Blocks.Add(b);
    }

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