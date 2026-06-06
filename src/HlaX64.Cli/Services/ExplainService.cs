using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;

namespace HlaX64.Cli.Services;

public sealed class ExplainReport
{
    public bool Success { get; init; }
    public string SourcePath { get; init; } = "";
    public string Target { get; init; } = "";
    public List<string> Diagnostics { get; init; } = [];
    public List<Diagnostic> StructuredDiagnostics { get; init; } = [];
    public List<IrFunction> IrFunctions { get; init; } = [];
    public List<LoweredFunction> LoweredFunctions { get; init; } = [];
    public string? Nasm { get; init; }

    public static ExplainReport Create(string sourcePath, string sourceText, CompilationOptions options)
    {
        var target = options.Target.ToString();
        var result = new Compilation(sourcePath, sourceText, options).Process();

        string? nasm = null;
        if (result.Success)
        {
            var emitter = new NasmEmitter();
            nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals);
        }

        return new ExplainReport
        {
            Success = result.Success,
            SourcePath = Path.GetFullPath(sourcePath),
            Target = target,
            Diagnostics = result.Diagnostics,
            StructuredDiagnostics = result.StructuredDiagnostics,
            IrFunctions = result.IrFunctions,
            LoweredFunctions = result.LoweredFunctions,
            Nasm = nasm
        };
    }

    public static string DescribeLowered(LoweredFunction fn)
    {
        var lines = new List<string> { $"function {fn.Name}:" };
        lines.Add($"  entry: {fn.IsEntryPoint}, export: {fn.IsExport}");
        lines.Add($"  stack frame: {fn.StackFrameSize} bytes");
        if (fn.Parameters.Count > 0)
            lines.Add($"  parameters: {string.Join(", ", fn.Parameters.Select(p => p.Name))}");
        if (fn.PreservedRegisters.Count > 0)
            lines.Add($"  preserved: {string.Join(", ", fn.PreservedRegisters)}");
        if (fn.RequiredExterns.Count > 0)
            lines.Add($"  externs: {string.Join(", ", fn.RequiredExterns)}");
        foreach (var block in fn.Blocks)
        {
            lines.Add($"  {block.Label}:");
            foreach (var inst in block.Instructions)
                lines.Add($"    {inst.AsmText}");
        }
        return string.Join('\n', lines);
    }
}
