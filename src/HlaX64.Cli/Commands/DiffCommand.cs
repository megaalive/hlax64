using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
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

        var oldSummary = Summarize(File.ReadAllText(settings.OldSource));
        var newSummary = Summarize(File.ReadAllText(settings.NewSource));

        var changedProcedures = newSummary.Procedures
            .Where(p => !oldSummary.Procedures.TryGetValue(p.Key, out var old) || old != p.Value)
            .Select(p => p.Key)
            .Concat(oldSummary.Procedures.Keys.Except(newSummary.Procedures.Keys))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        var addedExterns = newSummary.Externs.Except(oldSummary.Externs, StringComparer.OrdinalIgnoreCase).ToList();
        var removedExterns = oldSummary.Externs.Except(newSummary.Externs, StringComparer.OrdinalIgnoreCase).ToList();

        Report(settings, true, new
        {
            changedProcedures,
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

    private sealed record Summary(Dictionary<string, string> Procedures, List<string> Externs);

    private static Summary Summarize(string source)
    {
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var procedures = program.Statements
            .OfType<ProcedureNode>()
            .ToDictionary(
                p => p.Name,
                p => $"{p.Parameters.Count}:{p.ReturnsRegister ?? "rax"}",
                StringComparer.OrdinalIgnoreCase);
        var externs = program.Externs.OfType<ExternProcedureNode>().Select(e => e.Name).ToList();
        return new Summary(procedures, externs);
    }
}
