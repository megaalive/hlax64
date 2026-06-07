namespace HlaX64.AssemblyLab.Models;

public sealed class LabDiagnosticItem
{
    public int Line { get; init; }
    public string Code { get; init; } = "";
    public string Message { get; init; } = "";
    public string Severity { get; init; } = "error";

    public string Display => $"L{Line} {Code}: {Message}";
}
