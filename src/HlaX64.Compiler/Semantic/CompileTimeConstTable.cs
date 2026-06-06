namespace HlaX64.Compiler.Semantic;

/// <summary>
/// Compile-time integer constants resolved during semantic analysis.
/// </summary>
public sealed class CompileTimeConstTable
{
    private readonly Dictionary<string, long> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, long> Values => _values;

    public bool TryGetValue(string name, out long value) => _values.TryGetValue(name, out value);

    public void Define(string name, long value) => _values[name] = value;

    public CompileTimeConstTable Clone()
    {
        var copy = new CompileTimeConstTable();
        foreach (var (k, v) in _values)
            copy._values[k] = v;
        return copy;
    }
}
