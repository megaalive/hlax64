namespace HlaX64.Compiler.Types;

/// <summary>Function pointer / procedure signature type alias.</summary>
public sealed class ProcedureTypeSymbol
{
    public string Name { get; }
    public IReadOnlyList<(string ParamName, string ParamType)> Parameters { get; }
    public string ReturnType { get; }

    public ProcedureTypeSymbol(string name, IReadOnlyList<(string, string)> parameters, string returnType)
    {
        Name = name;
        Parameters = parameters;
        ReturnType = returnType;
    }
}
