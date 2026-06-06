namespace HlaX64.Compiler.Abi;

public sealed class LoweredFunction
{
    public string Name { get; }
    public List<LoweredBlock> Blocks { get; }
    public int StackFrameSize { get; set; }
    public List<string> PreservedRegisters { get; }
    public bool IsEntryPoint { get; set; }
    public bool IsExport { get; set; }
    public List<ParamInfo> Parameters { get; }

    public List<string> RequiredExterns { get; set; } = new();

    public LoweredFunction(string name, bool isEntryPoint = false)
    {
        Name = name;
        IsEntryPoint = isEntryPoint;
        Blocks = new List<LoweredBlock>();
        PreservedRegisters = new List<string>();
        Parameters = new List<ParamInfo>();
    }
}

public sealed class ParamInfo
{
    public string Name { get; }
    public int Index { get; }

    public ParamInfo(string name, int index)
    {
        Name = name;
        Index = index;
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

public sealed class StringLiteralInfo
{
    public string Label { get; }
    public string Value { get; }

    public StringLiteralInfo(string label, string value)
    {
        Label = label;
        Value = value;
    }
}