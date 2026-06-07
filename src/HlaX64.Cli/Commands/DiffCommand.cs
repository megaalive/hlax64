using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Parsing;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class DiffCommand : Command<DiffCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<old>")]
        public string OldSource { get; set; } = string.Empty;

        [CommandArgument(1, "<new>")]
        public string NewSource { get; set; } = string.Empty;

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.OldSource) || !File.Exists(settings.NewSource))
        {
            Report(settings, false, error: "Both source files must exist.");
            return 1;
        }

        var oldText = File.ReadAllText(settings.OldSource);
        var newText = File.ReadAllText(settings.NewSource);
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

        Report(settings, true, new
        {
            changedProcedures,
            returnRegisterChanges,
            stackFrameDeltas,
            addedExterns,
            removedExterns,
            oldProcedureCount = oldSummary.Procedures.Count,
            newProcedureCount = newSummary.Procedures.Count
        });
        return 0;
    }

    private static void Report(Settings settings, bool success, object? diff = null, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                diff,
                error
            });
            return;
        }

        if (!success)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        Console.WriteLine("Semantic diff complete (use --json for details).");
    }

    private sealed record ProcedureMeta(string Signature, string ReturnRegister, int StackFrameSize);

    private sealed record Summary(Dictionary<string, ProcedureMeta> Procedures, List<string> Externs);

    private static Summary Summarize(string source)
    {
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
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
