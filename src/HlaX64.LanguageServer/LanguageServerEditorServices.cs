using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Formatting;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.LanguageServer;

public static class LanguageServerEditorServices
{
    internal sealed record SymbolInfo(
        string Name,
        string Kind,
        int Line,
        int Column,
        int EndLine,
        int EndColumn,
        string? ContainerProcedure);

    private static readonly Dictionary<string, string> MnemonicHover = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mov"] = "Move/copy operand (HLA order: `mov(source, dest)`).",
        ["add"] = "Add source to destination.",
        ["sub"] = "Subtract source from destination.",
        ["xor"] = "Bitwise XOR; `xor(reg, reg)` clears register.",
        ["and"] = "Bitwise AND.",
        ["cmp"] = "Compare (sets flags).",
        ["lea"] = "Load effective address.",
        ["call"] = "Call procedure by name.",
    };

    private static readonly string[] Keywords =
    [
        "program", "begin", "end", "procedure", "var", "call", "export",
        "if", "else", "endif", "while", "endwhile", "do", "then",
        "include", "pragma", "target", "returns"
    ];

    private static readonly string[] Types =
    [
        "int64", "uint64", "int32", "uint32", "int16", "uint16", "int8", "uint8",
        "byte", "word", "dword", "qword", "ptr"
    ];

    private static readonly string[] Registers =
    [
        "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp",
        "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"
    ];

    private static readonly string[] Mnemonics =
    [
        "mov", "add", "sub", "imul", "xor", "and", "or", "cmp", "lea",
        "push", "pop", "inc", "dec", "neg", "not", "shl", "shr", "sar",
        "jmp", "ret", "syscall", "nop"
    ];

    public static object? GetHover(string source, int line, int character)
    {
        var word = GetWordAt(source, line, character);
        if (string.IsNullOrEmpty(word))
            return null;

        string? markdown = null;
        if (MnemonicHover.TryGetValue(word, out var hint))
            markdown = $"**{word}** — {hint}";
        else if (Keywords.Contains(word, StringComparer.OrdinalIgnoreCase))
            markdown = $"**{word}** — HlaX64 keyword.";
        else if (Types.Contains(word, StringComparer.OrdinalIgnoreCase))
            markdown = $"**{word}** — type name.";
        else if (Registers.Contains(word, StringComparer.OrdinalIgnoreCase))
            markdown = $"**{word}** — x64 register.";
        else if (word.StartsWith("HLAX", StringComparison.OrdinalIgnoreCase))
            markdown = $"Diagnostic code `{word}` — see docs/diagnostics.md.";
        else
        {
            var symbols = CollectSymbols(source);
            var enclosing = FindEnclosingProcedure(symbols, line);
            var match = FindSymbol(symbols, word, enclosing);
            if (match != null)
            {
                markdown = match.Kind switch
                {
                    "procedure" => $"**{match.Name}** — procedure.",
                    "parameter" => $"**{match.Name}** — parameter of `{match.ContainerProcedure}`.",
                    "variable" => $"**{match.Name}** — local variable in `{match.ContainerProcedure}`.",
                    _ => null
                };
            }
        }

        if (markdown == null)
            return null;

        return new
        {
            contents = new { kind = "markdown", value = markdown }
        };
    }

    public static object GetCompletions(string source, int line, int character)
    {
        var (prefix, _) = GetPrefixAt(source, line, character);
        var items = new List<object>();

        void Add(IEnumerable<string> labels, string kind)
        {
            foreach (var label in labels)
            {
                if (prefix.Length == 0 || label.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    items.Add(new
                    {
                        label,
                        kind,
                        detail = kind switch
                        {
                            "keyword" => "HlaX64 keyword",
                            "type" => "Type",
                            "register" => "Register",
                            _ => "Instruction"
                        }
                    });
                }
            }
        }

        Add(Keywords, "keyword");
        Add(Types, "type");
        Add(Registers, "register");
        Add(Mnemonics, "instruction");

        foreach (var symbol in CollectSymbols(source))
        {
            if (prefix.Length == 0 || symbol.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                items.Add(new
                {
                    label = symbol.Name,
                    kind = symbol.Kind switch
                    {
                        "procedure" => "function",
                        "parameter" => "variable",
                        _ => "variable"
                    },
                    detail = symbol.Kind switch
                    {
                        "procedure" => "Procedure",
                        "parameter" => $"Parameter ({symbol.ContainerProcedure})",
                        _ => $"Variable ({symbol.ContainerProcedure})"
                    }
                });
            }
        }

        return new { isIncomplete = false, items };
    }

    public static object? GetDefinition(string source, int line, int character, string uri)
    {
        var word = GetWordAt(source, line, character);
        if (string.IsNullOrEmpty(word))
            return null;

        var symbols = CollectSymbols(source);
        var enclosing = FindEnclosingProcedure(symbols, line);
        var match = FindSymbol(symbols, word, enclosing);
        if (match == null)
            return null;

        return new
        {
            uri,
            range = ToRange(match.Line, match.Column, match.EndLine, match.EndColumn)
        };
    }

    public static object[] GetDocumentSymbols(string source)
    {
        var symbols = CollectSymbols(source);
        var procedures = symbols.Where(s => s.Kind == "procedure").ToList();
        var result = new List<object>();

        foreach (var proc in procedures)
        {
            var children = symbols
                .Where(s => s.ContainerProcedure == proc.Name && s.Kind != "procedure")
                .Select(s => new
                {
                    name = s.Name,
                    kind = s.Kind == "parameter" ? 6 : 13,
                    range = ToRange(s.Line, s.Column, s.EndLine, s.EndColumn),
                    selectionRange = ToRange(s.Line, s.Column, s.Line, s.Column + s.Name.Length)
                })
                .Cast<object>()
                .ToArray();

            result.Add(new
            {
                name = proc.Name,
                kind = 12,
                range = ToRange(proc.Line, proc.Column, proc.EndLine, proc.EndColumn),
                selectionRange = ToRange(proc.Line, proc.Column, proc.Line, proc.Column + proc.Name.Length),
                children
            });
        }

        return result.ToArray();
    }

    public static object? FormatDocument(string source)
    {
        try
        {
            var formatted = AstFormatter.Format(source);
            var lines = source.Replace("\r\n", "\n").Split('\n');
            return new[]
            {
                new
                {
                    range = new
                    {
                        start = new { line = 0, character = 0 },
                        end = new { line = lines.Length, character = 0 }
                    },
                    newText = formatted
                }
            };
        }
        catch (ParseException)
        {
            return null;
        }
    }

    public static string GetWordAt(string source, int line, int character)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        if (line < 0 || line >= lines.Length)
            return "";

        var text = lines[line];
        if (character < 0) character = 0;
        if (character > text.Length) character = text.Length;

        var start = character;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;

        var end = character;
        while (end < text.Length && IsWordChar(text[end]))
            end++;

        return text[start..end];
    }

    private static List<SymbolInfo> CollectSymbols(string source)
    {
        try
        {
            var program = new Parser(new Lexer(source).Tokenize()).Parse();
            var symbols = new List<SymbolInfo>();

            foreach (var stmt in program.Statements.OfType<ProcedureNode>())
            {
                var endLine = stmt.Body.Count > 0 ? stmt.Body.Max(n => n.Line) : stmt.Line;
                symbols.Add(new SymbolInfo(
                    stmt.Name, "procedure", stmt.Line, stmt.Column, endLine, stmt.Column + stmt.Name.Length, null));

                foreach (var param in stmt.Parameters)
                {
                    symbols.Add(new SymbolInfo(
                        param.Name, "parameter", param.Line, param.Column,
                        param.Line, param.Column + param.Name.Length, stmt.Name));
                }

                foreach (var variable in stmt.Variables.OfType<VariableNode>())
                {
                    symbols.Add(new SymbolInfo(
                        variable.Name, "variable", variable.Line, variable.Column,
                        variable.Line, variable.Column + variable.Name.Length, stmt.Name));
                }
            }

            return symbols;
        }
        catch (ParseException)
        {
            return [];
        }
    }

    private static SymbolInfo? FindSymbol(IReadOnlyList<SymbolInfo> symbols, string name, string? enclosingProcedure)
    {
        SymbolInfo? match = null;
        foreach (var symbol in symbols)
        {
            if (!symbol.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (enclosingProcedure != null &&
                symbol.ContainerProcedure != null &&
                symbol.ContainerProcedure.Equals(enclosingProcedure, StringComparison.OrdinalIgnoreCase))
                return symbol;

            match ??= symbol;
        }

        return match;
    }

    private static string? FindEnclosingProcedure(IReadOnlyList<SymbolInfo> symbols, int line)
    {
        string? best = null;
        var bestLine = -1;
        foreach (var proc in symbols.Where(s => s.Kind == "procedure"))
        {
            if (line >= proc.Line && proc.Line >= bestLine)
            {
                best = proc.Name;
                bestLine = proc.Line;
            }
        }

        return best;
    }

    private static object ToRange(int startLine, int startCol, int endLine, int endCol) => new
    {
        start = new { line = Math.Max(0, startLine - 1), character = Math.Max(0, startCol - 1) },
        end = new { line = Math.Max(0, endLine - 1), character = Math.Max(0, endCol - 1) }
    };

    private static (string prefix, int startCol) GetPrefixAt(string source, int line, int character)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        if (line < 0 || line >= lines.Length)
            return ("", 0);

        var text = lines[line];
        if (character < 0) character = 0;
        if (character > text.Length) character = text.Length;

        var start = character;
        while (start > 0 && IsWordChar(text[start - 1]))
            start--;

        return (text[start..character], start);
    }

    private static bool IsWordChar(char c)
        => char.IsLetterOrDigit(c) || c == '_';
}
