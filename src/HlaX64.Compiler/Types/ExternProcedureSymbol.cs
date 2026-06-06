namespace HlaX64.Compiler.Types;

public sealed class ExternProcedureSymbol
{
    public string Name { get; init; } = "";
    public IReadOnlyList<(string ParamName, string ParamType)> Parameters { get; init; } =
        Array.Empty<(string, string)>();
    public string ReturnType { get; init; } = "void";
    public string? LinkLibrary { get; init; }
    public bool IsVariadic { get; init; }
}
