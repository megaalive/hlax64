namespace HlaX64.Compiler.Diagnostics;

/// <summary>
/// Represents a diagnostic message produced during compilation.
/// </summary>
public sealed class Diagnostic
{
    public string Code { get; }
    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public int Line { get; }
    public int Column { get; }
    public string? Suggestion { get; }

    public Diagnostic(string code, DiagnosticSeverity severity, string message, int line, int column, string? suggestion = null)
    {
        Code = code;
        Severity = severity;
        Message = message;
        Line = line;
        Column = column;
        Suggestion = suggestion;
    }

    public override string ToString()
    {
        var base_ = $"{Code}: {Message} at line {Line}, column {Column}";
        if (!string.IsNullOrEmpty(Suggestion))
        {
            base_ += $"\n  Did you mean '{Suggestion}'?";
        }
        return base_;
    }
}