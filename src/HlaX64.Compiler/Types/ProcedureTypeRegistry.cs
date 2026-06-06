namespace HlaX64.Compiler.Types;

public sealed class ProcedureTypeRegistry
{
    private readonly Dictionary<string, ProcedureTypeSymbol> _types =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Register(string name, IReadOnlyList<(string, string)> parameters, string returnType,
        out ProcedureTypeSymbol? symbol, out string? error)
    {
        symbol = null;
        error = null;

        if (_types.ContainsKey(name))
        {
            error = $"Duplicate procedure type alias '{name}'";
            return false;
        }

        symbol = new ProcedureTypeSymbol(name, parameters, returnType);
        _types[name] = symbol;
        return true;
    }

    public bool TryGet(string name, out ProcedureTypeSymbol symbol)
        => _types.TryGetValue(name, out symbol!);

    public bool Contains(string name) => _types.ContainsKey(name);

    public void Clear() => _types.Clear();
}
