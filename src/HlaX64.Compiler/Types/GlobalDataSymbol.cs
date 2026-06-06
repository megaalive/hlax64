using HlaX64.Compiler.Ast;

namespace HlaX64.Compiler.Types;

public sealed class GlobalDataSymbol
{
    public required string Name { get; init; }
    public required IntegerTypeSymbol Type { get; init; }
    public int ElementCount { get; init; } = 1;
    public long? InitialValue { get; init; }
    public bool InBss { get; init; }
    public int TotalBytes => (Type.BitWidth / 8) * ElementCount;
}

public sealed class GlobalDataRegistry
{
    private readonly Dictionary<string, GlobalDataSymbol> _globals = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, GlobalDataSymbol> Globals => _globals;

    public void Clear() => _globals.Clear();

    public bool Contains(string name) => _globals.ContainsKey(name);

    public bool TryGet(string name, out GlobalDataSymbol symbol)
        => _globals.TryGetValue(name, out symbol!);

    public bool Register(GlobalDataSymbol symbol)
    {
        if (_globals.ContainsKey(symbol.Name))
            return false;
        _globals[symbol.Name] = symbol;
        return true;
    }
}
