namespace HlaX64.Compiler.Options;

public enum OptimizationLevel
{
    None,
    Basic,
    Aggressive
}

public static class OptimizationLevelParser
{
    public static OptimizationLevel Parse(string? value) => value?.ToUpperInvariant() switch
    {
        "O1" or "1" or "BASIC" => OptimizationLevel.Basic,
        "O2" or "2" or "AGGRESSIVE" => OptimizationLevel.Aggressive,
        _ => OptimizationLevel.None
    };
}
