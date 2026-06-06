namespace HlaX64.Compiler.Ir;

public sealed class IrLocalLayout
{
    public int ElementCount { get; init; } = 1;
    public int ElementSizeBytes { get; init; } = 8;

    public int StackBytes => ElementCount * ElementSizeBytes;

    public int StackSlots => (StackBytes + 7) / 8;
}
