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

    private sealed class InstructionDatabaseDocument
    {
        [JsonPropertyName("instructions")]
        public List<InstructionInfo>? Instructions { get; set; }
    }
}
