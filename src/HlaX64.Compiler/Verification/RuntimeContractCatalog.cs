using System.Text.RegularExpressions;

namespace HlaX64.Compiler.Verification;

/// <summary>
/// One parsed <c>HLAX64-RUNTIME-FUNCTION v0.1</c> header from runtime NASM.
/// </summary>
public sealed record RuntimeFunctionContract(
    string Name,
    string? Target,
    IReadOnlyList<string> Clobbers,
    IReadOnlyList<string> Preserves);

/// <summary>
/// Catalog of runtime clobber contracts parsed from NASM headers.
/// Drives HLAX0076 when a call targets a known runtime symbol.
/// </summary>
public sealed partial class RuntimeContractCatalog
{
    private readonly Dictionary<string, RuntimeFunctionContract> _byName =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count => _byName.Count;

    public bool TryGet(string name, out RuntimeFunctionContract contract)
        => _byName.TryGetValue(NormalizeName(name), out contract!);

    public void Add(RuntimeFunctionContract contract)
        => _byName[NormalizeName(contract.Name)] = contract;

    public static RuntimeContractCatalog ParseText(string nasmText)
    {
        var catalog = new RuntimeContractCatalog();
        foreach (var contract in ParseContracts(nasmText))
            catalog.Add(contract);
        return catalog;
    }

    public static RuntimeContractCatalog LoadFromRuntimeRoot(string runtimeRoot)
    {
        var catalog = new RuntimeContractCatalog();
        if (string.IsNullOrWhiteSpace(runtimeRoot) || !Directory.Exists(runtimeRoot))
            return catalog;

        foreach (var nasm in Directory.EnumerateFiles(runtimeRoot, "*.nasm", SearchOption.AllDirectories))
        {
            string text;
            try
            {
                text = File.ReadAllText(nasm);
            }
            catch
            {
                continue;
            }

            foreach (var contract in ParseContracts(text))
                catalog.Add(contract);
        }

        return catalog;
    }

    public static string? TryFindRuntimeRoot()
    {
        var env = Environment.GetEnvironmentVariable("HLAX64_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return env;

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "HlaX64.Runtime");
            if (Directory.Exists(Path.Combine(candidate, "linux-x64")) ||
                Directory.Exists(Path.Combine(candidate, "windows-x64")))
                return candidate;

            var bundled = Path.Combine(dir.FullName, "runtime");
            if (Directory.Exists(Path.Combine(bundled, "linux-x64")) ||
                Directory.Exists(Path.Combine(bundled, "windows-x64")))
                return bundled;

            dir = dir.Parent;
        }

        return null;
    }

    private static IEnumerable<RuntimeFunctionContract> ParseContracts(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (!HeaderMarker().IsMatch(lines[i]))
                continue;

            string? name = null;
            string? target = null;
            var clobbers = new List<string>();
            var preserves = new List<string>();
            string? listField = null;

            for (var j = i + 1; j < lines.Length; j++)
            {
                var raw = lines[j];
                var line = raw.Trim();
                if (line.Length == 0)
                    continue;
                if (!line.StartsWith(';'))
                    break;
                if (HeaderMarker().IsMatch(line))
                {
                    i = j - 1;
                    break;
                }

                var body = line.TrimStart(';').Trim();
                if (body.StartsWith("name:", StringComparison.OrdinalIgnoreCase))
                {
                    name = body["name:".Length..].Trim();
                    listField = null;
                    continue;
                }
                if (body.StartsWith("target:", StringComparison.OrdinalIgnoreCase))
                {
                    target = body["target:".Length..].Trim();
                    listField = null;
                    continue;
                }
                if (body.StartsWith("clobbers:", StringComparison.OrdinalIgnoreCase))
                {
                    listField = "clobbers";
                    AppendRegs(clobbers, body["clobbers:".Length..]);
                    continue;
                }
                if (body.StartsWith("preserves:", StringComparison.OrdinalIgnoreCase))
                {
                    listField = "preserves";
                    AppendRegs(preserves, body["preserves:".Length..]);
                    continue;
                }
                if (body.StartsWith("inputs:", StringComparison.OrdinalIgnoreCase) ||
                    body.StartsWith("stack-align:", StringComparison.OrdinalIgnoreCase) ||
                    body.StartsWith("notes:", StringComparison.OrdinalIgnoreCase) ||
                    body.StartsWith("returns:", StringComparison.OrdinalIgnoreCase))
                {
                    listField = null;
                    continue;
                }

                if (listField == "clobbers")
                    AppendRegs(clobbers, body);
                else if (listField == "preserves")
                    AppendRegs(preserves, body);
            }

            if (!string.IsNullOrWhiteSpace(name) && clobbers.Count > 0)
            {
                yield return new RuntimeFunctionContract(
                    name,
                    target,
                    clobbers,
                    preserves);
            }
        }
    }

    private static void AppendRegs(List<string> dest, string fragment)
    {
        foreach (Match m in RegToken().Matches(fragment))
        {
            var reg = m.Value.ToLowerInvariant();
            if (!dest.Contains(reg, StringComparer.Ordinal))
                dest.Add(reg);
        }
    }

    private static string NormalizeName(string name)
    {
        var n = name.Trim();
        if (n.StartsWith("extern:", StringComparison.OrdinalIgnoreCase))
            n = n["extern:".Length..];
        return n;
    }

    [GeneratedRegex(@"^\s*;\s*HLAX64-RUNTIME-FUNCTION\s+v0\.1\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex HeaderMarker();

    [GeneratedRegex(@"\b(r(?:[89]|1[0-5]|ax|bx|cx|dx|si|di|sp|bp)|e(?:ax|bx|cx|dx|si|di|sp|bp)|[abcd][xlh]|sil|dil|bpl|spl)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RegToken();
}
