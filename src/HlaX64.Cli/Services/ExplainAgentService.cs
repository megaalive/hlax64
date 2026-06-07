using System.Text.Json;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Options;

namespace HlaX64.Cli.Services;

public sealed class ExplainAgentService
{
    private static readonly JsonSerializerOptions JsonOpts = CliJson.Options;

    public static object ExplainForAgent(string sourcePath, string sourceText, string? target = null)
    {
        var triple = TargetTriple.Parse(target ?? "linux-x64-sysv");
        var options = CompilationOptions.Default with { Target = triple };
        var report = ExplainReport.Create(sourcePath, sourceText, options);
        var verify = ExplainReport.CollectVerificationWarnings(sourceText, options);

        return new
        {
            schemaVersion = CliJson.SchemaVersion,
            success = report.Success,
            version = Compilation.GetVersion(),
            source = report.SourcePath,
            target = target ?? "linux-x64-sysv",
            diagnostics = report.StructuredDiagnostics.Select(d => new
            {
                code = d.Code,
                message = d.Message,
                line = d.Line,
                column = d.Column,
                span = new { d.Line, d.Column },
                suggestedFix = SuggestFix(d)
            }),
            abiIssues = verify.AbiIssues,
            clobberWarnings = verify.ClobberWarnings,
            ir = report.Success ? report.IrFunctions.Select(f => f.ToString()).ToArray() : null,
            lowered = report.Success ? report.LoweredFunctions.Select(ExplainReport.DescribeLowered).ToArray() : null,
            nasm = report.Nasm
        };
    }

    public static string ExplainForAgentJson(string sourcePath, string sourceText, string? target = null)
        => JsonSerializer.Serialize(ExplainForAgent(sourcePath, sourceText, target), JsonOpts);

    public static object? SuggestFix(Diagnostic d)
    {
        if (d.Suggestion != null)
        {
            return new
            {
                template = $"Replace with '{d.Suggestion}'",
                replacement = d.Suggestion,
                line = d.Line,
                column = d.Column,
                applyKind = "replaceToken"
            };
        }

        if (d.Code == "HLAX0003" || d.Code == "HLAX0071")
            return new { template = "Check mnemonic spelling or run `hla64 list-instructions`." };
        if (d.Code == "HLAX0070")
            return new { template = "Add `--features +sse2` (or required feature) to build flags." };
        if (d.Code == "HLAX0060")
            return new { template = "Initialize `{var}` before use or assign in all paths." };
        return null;
    }
}
