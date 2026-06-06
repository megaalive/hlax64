namespace HlaX64.Compiler.Options;

public enum OptimizationLevel
{
    None,
    Basic
}

public static class OptimizationLevelParser
{
    public static OptimizationLevel Parse(string? value) => value?.ToUpperInvariant() switch
    {
        "O1" or "1" or "BASIC" => OptimizationLevel.Basic,
        _ => OptimizationLevel.None
    };
}
