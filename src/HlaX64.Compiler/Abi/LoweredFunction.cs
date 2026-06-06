namespace HlaX64.Compiler.Abi;

public sealed class LoweredFunction
{
    public string Name { get; }
    public List<LoweredBlock> Blocks { get; }
    public int StackFrameSize { get; set; }
    public List<string> PreservedRegisters { get; }

    public LoweredFunction(string name)
    {
        Name = name;
        Blocks = new List<LoweredBlock>();
        PreservedRegisters = new List<string>();
    }
}

public sealed class LoweredBlock
{
    public string Label { get; set; }
    public List<LoweredInstruction> Instructions { get; }

    public LoweredBlock(string label)
    {
        Label = label;
        Instructions = new List<LoweredInstruction>();
    }
}

public sealed class LoweredInstruction
{
    public string AsmText { get; set; }

    public LoweredInstruction(string asmText)
    {
        AsmText = asmText;
    }
}
