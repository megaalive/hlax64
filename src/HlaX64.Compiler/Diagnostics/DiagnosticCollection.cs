namespace HlaX64.Compiler.Diagnostics;

/// <summary>
/// Collection of diagnostics produced during compilation.
/// </summary>
public sealed class DiagnosticCollection
{
    private readonly List<Diagnostic> _diagnostics = new();

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics;
    public bool HasErrors => _diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error);

    public void Report(string code, DiagnosticSeverity severity, string message, int line, int column, string? suggestion = null)
    {
        _diagnostics.Add(new Diagnostic(code, severity, message, line, column, suggestion));
    }

    public void Error(string code, string message, int line, int column, string? suggestion = null)
    {
        Report(code, DiagnosticSeverity.Error, message, line, column, suggestion);
    }

    public void Warning(string code, string message, int line, int column, string? suggestion = null)
    {
        Report(code, DiagnosticSeverity.Warning, message, line, column, suggestion);
    }

    public void Info(string code, string message, int line, int column, string? suggestion = null)
    {
        Report(code, DiagnosticSeverity.Info, message, line, column, suggestion);
    }

    public void Clear()
    {
        _diagnostics.Clear();
    }
}