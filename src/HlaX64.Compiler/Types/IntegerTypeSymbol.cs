namespace HlaX64.Compiler.Types;

public sealed record IntegerTypeSymbol(string Name, int BitWidth, bool IsSigned)
{
    public TypeKind Kind => TypeKind.Integer;

    public long MinValue => IsSigned
        ? -1L << (BitWidth - 1)
        : 0;

    public long MaxValue => IsSigned
        ? (1L << (BitWidth - 1)) - 1
        : (1L << BitWidth) - 1;
}
