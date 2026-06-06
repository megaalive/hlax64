namespace HlaX64.Compiler.Types;

public sealed class ExternProcedureRegistry
{
    private readonly Dictionary<string, ExternProcedureSymbol> _procedures =
        new(StringComparer.OrdinalIgnoreCase);

    public bool Register(ExternProcedureSymbol symbol, out string? error)
    {
        error = null;
        if (_procedures.ContainsKey(symbol.Name))
        {
            error = $"Duplicate extern procedure '{symbol.Name}'";
            return false;
        }

        _procedures[symbol.Name] = symbol;
        return true;
    }

    public bool TryGet(string name, out ExternProcedureSymbol symbol)
        => _procedures.TryGetValue(name, out symbol!);

    public bool Contains(string name) => _procedures.ContainsKey(name);

    public void Clear() => _procedures.Clear();

    public IEnumerable<ExternProcedureSymbol> All => _procedures.Values;

    public IEnumerable<string> ResolveLinkLibraries(bool isWindows)
    {
        var libs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var proc in _procedures.Values)
        {
            if (string.IsNullOrWhiteSpace(proc.LinkLibrary))
                continue;

            var lib = NormalizeLinkLibrary(proc.LinkLibrary, isWindows);
            if (lib != null)
                libs.Add(lib);
        }

        return libs;
    }

    public static string? NormalizeLinkLibrary(string fromClause, bool isWindows)
    {
        var name = fromClause.Trim().Trim('"');
        if (string.IsNullOrEmpty(name))
            return null;

        if (isWindows)
        {
            if (name.EndsWith(".lib", StringComparison.OrdinalIgnoreCase))
                return name;
            if (name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return Path.GetFileNameWithoutExtension(name) + ".lib";
            return name + ".lib";
        }

        // Linux SysV
        if (name is "libc.so" or "libc.so.6" or "libc")
            return "-lc";
        if (name.StartsWith("-l", StringComparison.Ordinal))
            return name;
        if (name.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
            return "-l" + name[..^3];
        return "-l" + name;
    }
}
