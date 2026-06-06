using HlaX64.Compiler;
using HlaX64.Compiler.Diagnostics;

namespace HlaX64.LanguageServer;

internal static class DocumentDiagnostics
{
    public static object[] ToLsp(string uri, string source)
    {
        var analysis = DiagnosticService.Analyze(source);
        return analysis.Diagnostics.Select(d => new
        {
            range = new
            {
                start = new { line = Math.Max(0, d.Line - 1), character = Math.Max(0, d.Column - 1) },
                end = new { line = Math.Max(0, d.Line - 1), character = Math.Max(0, d.Column) }
            },
            severity = d.Severity switch
            {
                DiagnosticSeverity.Error => 1,
                DiagnosticSeverity.Warning => 2,
                _ => 3
            },
            code = d.Code,
            source = "hla64",
            message = d.Suggestion != null ? $"{d.Message} (Did you mean '{d.Suggestion}'?)" : d.Message
        }).ToArray<object>();
    }

    public static void Publish(string uri, string source, Action<object> sendNotification)
    {
        sendNotification(new
        {
            jsonrpc = "2.0",
            method = "textDocument/publishDiagnostics",
            @params = new
            {
                uri,
                diagnostics = ToLsp(uri, source)
            }
        });
    }
}
