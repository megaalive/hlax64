using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Ir;

public sealed class IrValue
{
    private static int _nextId;

    public int Id { get; }
    public IntegerTypeSymbol? Type { get; }

    public IrValue(IntegerTypeSymbol? type)
    {
        Id = _nextId++;
        Type = type;
    }

    public override string ToString() => $"v{Id}{(Type != null ? $":{Type.Name}" : "")}";
}
