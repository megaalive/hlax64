namespace HlaX64.Compiler;

/// <summary>
/// Represents the main compilation entry point for HlaX64.
/// Coordinates lexing, parsing, semantic analysis, and backend emission.
/// </summary>
public class Compilation
{
    public string SourcePath { get; }
    public string SourceText { get; }

    public Compilation(string sourcePath, string sourceText)
    {
        SourcePath = sourcePath;
        SourceText = sourceText;
    }

    public static string GetVersion()
    {
        return "0.1.0-alpha";
    }
}