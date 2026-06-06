using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler;

public sealed class CompilationResult
{
    public bool Success { get; set; }
    public List<IrFunction> IrFunctions { get; set; } = new();
    public List<LoweredFunction> LoweredFunctions { get; set; } = new();
    public List<StringLiteralInfo> StringLiterals { get; set; } = new();
    public List<string> Diagnostics { get; set; } = new();
}

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

    public CompilationResult Process()
    {
        var result = new CompilationResult();

        try
        {
            // 1. Lex
            var lexer = new Lexer(SourceText);
            var tokens = lexer.Tokenize();

            // 2. Parse
            var parser = new Parser(tokens);
            var program = parser.Parse();

            // 3. Semantic analysis
            var semantic = new SemanticAnalyzer();
            var semDiags = semantic.Analyze(program);
            if (semDiags.HasErrors)
            {
                foreach (var diag in semDiags.Diagnostics)
                    result.Diagnostics.Add($"Semantic error: {diag.Message}");
                result.Success = false;
                return result;
            }

            // 4. Lower AST to IR
            var lowering = new AstToIrLowering();
            var procedures = new List<IrFunction>();
            var entryIr = lowering.LowerProgram(program, procedures);

            result.IrFunctions.Add(entryIr);
            result.IrFunctions.AddRange(procedures);

            // 5. Lower IR to ABI-specific form
            var abiLowerer = Options.Target.Abi.ToLowerInvariant() switch
            {
                "sysv" => new SysVAbiLowerer(),
                _ => new SysVAbiLowerer()
            };

            foreach (var irFunc in result.IrFunctions)
            {
                var lowered = abiLowerer.Lower(irFunc, Options);
                result.LoweredFunctions.Add(lowered);
            }

            result.StringLiterals = abiLowerer.StringLiterals.ToList();
            result.Success = true;
        }
        catch (ParseException ex)
        {
            result.Diagnostics.Add($"Parse error: {ex.Message}");
        }
        catch (Exception ex)
        {
            result.Diagnostics.Add($"Error: {ex.Message}");
        }

        return result;
    }
}