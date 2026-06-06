namespace HlaX64.Compiler.Cpu;

public sealed class CpuFeatureSet
{
    public string Baseline { get; init; } = "baseline-x64";
    public HashSet<string> Enabled { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> Disabled { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static CpuFeatureSet BaselineX64 { get; } = new()
    {
        Baseline = "baseline-x64"
    };

    public static CpuFeatureSet Parse(string? baseline, IEnumerable<string>? featureFlags)
    {
        var set = new CpuFeatureSet { Baseline = baseline ?? "baseline-x64" };
        if (featureFlags == null) return set;

        foreach (var flag in featureFlags)
        {
            var trimmed = flag.Trim();
            if (trimmed.StartsWith('+'))
                set.Enabled.Add(trimmed[1..]);
            else if (trimmed.StartsWith('-'))
                set.Disabled.Add(trimmed[1..]);
            else if (trimmed.Length > 0)
                set.Enabled.Add(trimmed);
        }

        foreach (var disabled in set.Disabled)
            set.Enabled.Remove(disabled);

        return set;
    }

    public bool HasFeature(string feature)
    {
        if (Disabled.Contains(feature)) return false;
        if (Enabled.Contains(feature)) return true;
        return feature.Equals("sse2", StringComparison.OrdinalIgnoreCase) &&
               Baseline.Equals("baseline-x64", StringComparison.OrdinalIgnoreCase);
    }
}
