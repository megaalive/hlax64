using System.Text.Json;
using System.Text.Json.Serialization;

namespace HlaX64.Compiler.Cpu;

public sealed class InstructionInfo
{
    [JsonPropertyName("mnemonic")]
    public string Mnemonic { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("minOps")]
    public int MinOps { get; set; }

    [JsonPropertyName("maxOps")]
    public int MaxOps { get; set; }

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = [];
}

public sealed class InstructionDatabase
{
    private readonly Dictionary<string, InstructionInfo> _byMnemonic = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<InstructionInfo> All => _byMnemonic.Values;

    public static InstructionDatabase LoadDefault()
    {
        var db = new InstructionDatabase();
        var path = FindDatabasePath();
        if (path != null && File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var doc = JsonSerializer.Deserialize<InstructionDatabaseDocument>(json);
            if (doc?.Instructions != null)
            {
                foreach (var info in doc.Instructions)
                    db._byMnemonic[info.Mnemonic] = info;
            }
        }

        if (db._byMnemonic.Count == 0)
            db.LoadFallback();
        return db;
    }

    private static string? FindDatabasePath()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "data", "instructions.json"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data", "instructions.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "data", "instructions.json")
        };
        foreach (var c in candidates)
        {
            var full = Path.GetFullPath(c);
            if (File.Exists(full)) return full;
        }
        return null;
    }

    private void LoadFallback()
    {
        foreach (var info in new[]
        {
            new InstructionInfo { Mnemonic = "mov", Category = "data", MinOps = 2, MaxOps = 2 },
            new InstructionInfo { Mnemonic = "add", Category = "arith", MinOps = 2, MaxOps = 2 },
            new InstructionInfo { Mnemonic = "addsd", Category = "simd", MinOps = 2, MaxOps = 2, Features = ["sse2"] },
            new InstructionInfo { Mnemonic = "ucomisd", Category = "simd", MinOps = 2, MaxOps = 2, Features = ["sse2"] }
        })
            _byMnemonic[info.Mnemonic] = info;
    }

    public bool TryGet(string mnemonic, out InstructionInfo? info)
        => _byMnemonic.TryGetValue(mnemonic, out info);

    public IEnumerable<string> Mnemonics => _byMnemonic.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase);

    public string? SuggestClosest(string mnemonic)
    {
        string? best = null;
        var bestDist = int.MaxValue;
        foreach (var known in _byMnemonic.Keys)
        {
            var dist = LevenshteinDistance(mnemonic, known);
            if (dist < bestDist && dist <= 3)
            {
                bestDist = dist;
                best = known;
            }
        }
        return best;
    }

    private static int LevenshteinDistance(string s, string t)
    {
        var d = new int[s.Length + 1, t.Length + 1];
        for (int i = 0; i <= s.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= t.Length; j++) d[0, j] = j;
        for (int i = 1; i <= s.Length; i++)
        {
            for (int j = 1; j <= t.Length; j++)
            {
                var cost = char.ToLowerInvariant(s[i - 1]) == char.ToLowerInvariant(t[j - 1]) ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }
        return d[s.Length, t.Length];
    }

    private sealed class InstructionDatabaseDocument
    {
        [JsonPropertyName("instructions")]
        public List<InstructionInfo>? Instructions { get; set; }
    }
}
