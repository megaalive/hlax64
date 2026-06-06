using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler;

/// <summary>
/// Runs lexer, parser, and semantic analysis without code generation.
/// </summary>
public static class DiagnosticService
{
    public sealed class AnalysisResult
    {
        public bool Success { get; init; }
        public List<Diagnostic> Diagnostics { get; init; } = [];
        public string? ParseError { get; init; }
    }

    public static AnalysisResult Analyze(string sourceText, CompilerWarnings? warnings = null)
    {
        try
        {
            var lexer = new Lexer(sourceText);
            var parser = new Parser(lexer.Tokenize());
            var program = parser.Parse();
            var sem = new SemanticAnalyzer(warnings ?? new CompilerWarnings(Bounds: true));
            var diags = sem.Analyze(program);
            return new AnalysisResult
            {
                Success = !diags.HasErrors,
                Diagnostics = diags.Diagnostics.ToList()
            };
        }
        catch (ParseException ex)
        {
            var line = ex.Line > 0 ? ex.Line : 1;
            var column = ex.Column > 0 ? ex.Column : 1;
            return new AnalysisResult
            {
                Success = false,
                ParseError = ex.Message,
                Diagnostics =
                [
                    new Diagnostic("HLAX1000", DiagnosticSeverity.Error, ex.Message, line, column)
                ]
            };
        }
    }
}
