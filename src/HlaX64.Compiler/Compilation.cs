using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Optimization;
using HlaX64.Compiler.Parsing;
using HlaX64.Compiler.Semantic;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler;

public sealed class CompilationResult
{
    public bool Success { get; set; }
    public List<IrFunction> IrFunctions { get; set; } = new();
    public List<LoweredFunction> LoweredFunctions { get; set; } = new();
    public List<StringLiteralInfo> StringLiterals { get; set; } = new();
    public List<GlobalDataSymbol> GlobalData { get; set; } = new();
    public List<string> LinkLibraries { get; set; } = new();
    public ExternProcedureRegistry ExternProcedures { get; set; } = new();
    public ProcedureTypeRegistry ProcedureTypes { get; set; } = new();
    public RecordTypeRegistry RecordTypes { get; set; } = new();
    public List<string> Diagnostics { get; set; } = new();
    public List<Diagnostic> StructuredDiagnostics { get; set; } = new();
    public Debug.SourceMapDocument? SourceMap { get; set; }
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
        return "0.2.1-alpha";
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
            var semantic = new SemanticAnalyzer(Options.Warnings, Options.CpuFeatures);
            var semDiags = semantic.Analyze(program);
            foreach (var diag in semDiags.Diagnostics)
            {
                result.StructuredDiagnostics.Add(diag);
                result.Diagnostics.Add(diag.ToString());
            }

            if (semDiags.HasErrors)
            {
                result.Success = false;
                return result;
            }

            if (Options.Warnings.DefiniteAssignment || Options.Warnings.Unreachable || Options.Warnings.Liveness)
            {
                var verification = new Verification.VerificationAnalyzer(Options.Warnings);
                var verifyDiags = verification.Analyze(program);
                foreach (var diag in verifyDiags.Diagnostics)
                {
                    result.StructuredDiagnostics.Add(diag);
                    result.Diagnostics.Add(diag.ToString());
                }
            }

            // 4. Lower AST to IR
            var lowering = new AstToIrLowering(semantic.ConstTable, semantic.RecordTypes, semantic.GlobalData,
                semantic.ExternProcedures, semantic.ProcedureTypes);
            var procedures = new List<IrFunction>();
            var entryIr = lowering.LowerProgram(program, procedures);

            result.IrFunctions.Add(entryIr);
            result.IrFunctions.AddRange(procedures);

            // 4b. IR optimization
            IrOptimizer.Optimize(result.IrFunctions, Options.Optimization);

            result.GlobalData = semantic.GlobalData.Globals.Values.ToList();
            result.ExternProcedures = semantic.ExternProcedures;
            result.ProcedureTypes = semantic.ProcedureTypes;
            result.RecordTypes = semantic.RecordTypes;
            result.LinkLibraries = semantic.ExternProcedures
                .ResolveLinkLibraries(Options.Target.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // 5. Lower IR to ABI-specific form
            IAbiLowerer abiLowerer;
            switch (Options.Target.Abi.ToLowerInvariant())
            {
                case "msabi":
                    abiLowerer = new WindowsMsAbiLowerer();
                    break;
                default:
                    abiLowerer = new SysVAbiLowerer();
                    break;
            }

            var globalMap = semantic.GlobalData.Globals;
            var procTypes = semantic.ProcedureTypes;
            var recordTypes = semantic.RecordTypes;
            foreach (var irFunc in result.IrFunctions)
            {
                var lowered = abiLowerer.Lower(irFunc, Options, globalMap, procTypes, recordTypes, semantic.ExternProcedures);
                result.LoweredFunctions.Add(lowered);
            }

            if (Options.Optimization != OptimizationLevel.None)
                PeepholeOptimizer.OptimizeLowered(result.LoweredFunctions, Options.Optimization);

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