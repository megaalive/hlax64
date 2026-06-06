namespace HlaX64.Compiler.Types;

public sealed record FloatTypeSymbol(string Name, int BitWidth)
{
    public TypeKind Kind => TypeKind.Float;
}
