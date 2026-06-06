namespace HlaX64.Compiler.Ir;

public sealed class IrValue
{
    private static int _nextId;

    public int Id { get; }
    public string? Name { get; set; }

    public IrValue()
    {
        Id = _nextId++;
    }

    public override string ToString() => $"v{Id}";
}
