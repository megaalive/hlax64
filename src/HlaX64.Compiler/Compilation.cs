using HlaX64.Compiler.Options;

namespace HlaX64.Compiler;

public class Compilation
{
    public string SourcePath { get; }
    public string SourceText { get; }
    public CompilationOptions Options { get; }

    public Compilation(string sourcePath, string sourceText, CompilationOptions? options = null)
    {
        SourcePath = sourcePath;
        SourceText = sourceText;
        Options = options ?? CompilationOptions.Default;
    }

    public static string GetVersion()
    {
        return "0.1.0-alpha";
    }
}
