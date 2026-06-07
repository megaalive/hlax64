using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Cli.Services;

public static class SemanticDiffService
{
    public static object Compare(string oldText, string newText)
    {
        var oldSummary = Summarize(oldText);
        var newSummary = Summarize(newText);

        var changedProcedures = newSummary.Procedures.Keys
            .Concat(oldSummary.Procedures.Keys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(name =>
            {
                oldSummary.Procedures.TryGetValue(name, out var oldP);
                newSummary.Procedures.TryGetValue(name, out var newP);
                return oldP == null || newP == null || !oldP.Signature.Equals(newP.Signature, StringComparison.Ordinal);
            })
            .OrderBy(x => x)
            .ToList();

        var returnRegisterChanges = changedProcedures
            .Where(name => oldSummary.Procedures.TryGetValue(name, out var o) &&
                           newSummary.Procedures.TryGetValue(name, out var n) &&
                           o!.ReturnRegister != n!.ReturnRegister)
            .Select(name => new
            {
                procedure = name,
                oldReturn = oldSummary.Procedures[name].ReturnRegister,
                newReturn = newSummary.Procedures[name].ReturnRegister
            })
            .ToList();

        var stackFrameDeltas = changedProcedures
            .Where(name => oldSummary.Procedures.TryGetValue(name, out var o) &&
                           newSummary.Procedures.TryGetValue(name, out var n) &&
                           o!.StackFrameSize != n!.StackFrameSize)
            .Select(name => new
            {
                procedure = name,
                oldStackFrame = oldSummary.Procedures[name].StackFrameSize,
                newStackFrame = newSummary.Procedures[name].StackFrameSize,
                delta = newSummary.Procedures[name].StackFrameSize - oldSummary.Procedures[name].StackFrameSize
            })
            .ToList();

        var addedExterns = newSummary.Externs.Except(oldSummary.Externs, StringComparer.OrdinalIgnoreCase).ToList();
        var removedExterns = oldSummary.Externs.Except(newSummary.Externs, StringComparer.OrdinalIgnoreCase).ToList();

        return new
        {
            changedProcedures,
            returnRegisterChanges,
            stackFrameDeltas,
            addedExterns,
            removedExterns,
            oldProcedureCount = oldSummary.Procedures.Count,
            newProcedureCount = newSummary.Procedures.Count
        };
    }

    public static string FormatDiffText(string oldText, string newText)
    {
        var diff = Compare(oldText, newText);
        return System.Text.Json.JsonSerializer.Serialize(diff, HlaX64.Cli.Json.CliJson.Options);
    }

    private sealed record ProcedureMeta(string Signature, string ReturnRegister, int StackFrameSize);

    private sealed record Summary(Dictionary<string, ProcedureMeta> Procedures, List<string> Externs);

    private static Summary Summarize(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return new Summary(new Dictionary<string, ProcedureMeta>(), []);

        ProgramNode program;
        try
        {
            program = new Parser(new Lexer(source).Tokenize()).Parse();
        }
        catch (ParseException)
        {
            return new Summary(new Dictionary<string, ProcedureMeta>(), []);
        }
        var compile = new Compilation("(diff)", source, CompilationOptions.Default).Process();
        var lowered = compile.LoweredFunctions.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);

        var procedures = program.Statements
            .OfType<ProcedureNode>()
            .ToDictionary(
                p => p.Name,
                p =>
                {
                    lowered.TryGetValue(p.Name, out var lf);
                    var sig = $"{p.Parameters.Count}:{p.ReturnsRegister ?? "rax"}";
                    return new ProcedureMeta(sig, p.ReturnsRegister ?? "rax", lf?.StackFrameSize ?? 0);
                },
                StringComparer.OrdinalIgnoreCase);

        var externs = program.Externs.OfType<ExternProcedureNode>().Select(e => e.Name).ToList();
        return new Summary(procedures, externs);
    }
}
