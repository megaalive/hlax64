using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Analysis;

public sealed class CapabilityManifest
{
    public bool FilesystemAccess { get; set; }
    public bool HasStdoutPut { get; set; }
    public bool HasExtern { get; set; }
    public List<string> Syscalls { get; init; } = [];
    public List<string> ExternalLibraries { get; init; } = [];
    public List<string> ExternProcedures { get; init; } = [];
    public List<string> FileRelatedExterns { get; init; } = [];
}

public static class CapabilityAnalyzer
{
    private static readonly HashSet<string> FileExternHints = new(StringComparer.OrdinalIgnoreCase)
    {
        "open", "close", "read", "write", "access", "fopen", "fclose", "fread", "fwrite", "puts", "printf"
    };

    public static CapabilityManifest Analyze(string sourceText)
    {
        var manifest = new CapabilityManifest();
        var tokens = new Lexer(sourceText).Tokenize();
        var program = new Parser(tokens).Parse();

        foreach (var ext in program.Externs.OfType<ExternProcedureNode>())
        {
            manifest.HasExtern = true;
            manifest.ExternProcedures.Add(ext.Name);
            if (!string.IsNullOrEmpty(ext.LinkLibrary))
                manifest.ExternalLibraries.Add(ext.LinkLibrary);

            if (FileExternHints.Contains(ext.Name) ||
                ext.Name.Contains("file", StringComparison.OrdinalIgnoreCase) ||
                ext.Name.Contains("read", StringComparison.OrdinalIgnoreCase) ||
                ext.Name.Contains("write", StringComparison.OrdinalIgnoreCase))
            {
                manifest.FilesystemAccess = true;
                manifest.FileRelatedExterns.Add(ext.Name);
            }
        }

        foreach (var stmt in EnumerateStatements(program))
        {
            if (stmt is InstructionNode instr &&
                instr.Mnemonic.Equals("syscall", StringComparison.OrdinalIgnoreCase))
            {
                if (!manifest.Syscalls.Contains("generic"))
                    manifest.Syscalls.Add("generic");
            }

            if (stmt is CallNode call && call.Name is "stdout.put" or "stdout.putu")
                manifest.HasStdoutPut = true;
        }

        if (sourceText.Contains("stdout.put", StringComparison.Ordinal)
            || sourceText.Contains("stdout.putu", StringComparison.Ordinal))
        {
            manifest.HasStdoutPut = true;
            if (!manifest.Syscalls.Contains("write"))
                manifest.Syscalls.Add("write");
            if (!manifest.Syscalls.Contains("exit"))
                manifest.Syscalls.Add("exit");
        }

        return manifest;
    }

    private static IEnumerable<AstNode> EnumerateStatements(ProgramNode program)
    {
        foreach (var stmt in program.Statements)
        {
            foreach (var inner in Walk(stmt))
                yield return inner;
        }
    }

    private static IEnumerable<AstNode> Walk(AstNode node)
    {
        yield return node;
        switch (node)
        {
            case ProcedureNode proc:
                foreach (var s in proc.Body)
                    foreach (var inner in Walk(s))
                        yield return inner;
                break;
            case BlockNode block:
                foreach (var s in block.Statements)
                    foreach (var inner in Walk(s))
                        yield return inner;
                break;
            case IfNode ifNode:
                foreach (var s in ifNode.ThenBody)
                    foreach (var inner in Walk(s))
                        yield return inner;
                foreach (var s in ifNode.ElseBody)
                    foreach (var inner in Walk(s))
                        yield return inner;
                break;
            case WhileNode whileNode:
                foreach (var s in whileNode.Body)
                    foreach (var inner in Walk(s))
                        yield return inner;
                break;
        }
    }
}
