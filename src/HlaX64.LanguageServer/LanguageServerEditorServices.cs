using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Formatting;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Options;
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
        "program", "begin", "end", "procedure", "var", "call", "export", "extern", "type", "from", "variadic",
        "if", "else", "endif", "while", "endwhile", "do", "then",
        "include", "pragma", "target", "returns"
    ];

    private static readonly string[] Types =
    [
        "int64", "uint64", "int32", "uint32", "int16", "uint16", "int8", "uint8",
        "byte", "word", "dword", "qword", "ptr", "float32", "float64", "real32", "real64", "cstring"
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

    public static object? GetSignatureHelp(string source, int line, int character)
    {
        var callSite = FindCallSite(source, line, character);
        if (callSite == null)
        {
            var word = GetWordAt(source, line, character);
            if (!string.IsNullOrEmpty(word))
            {
                var lines = source.Replace("\r\n", "\n").Split('\n');
                if (line >= 0 && line < lines.Length &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        lines[line], $@"call\s+{System.Text.RegularExpressions.Regex.Escape(word)}\s*\(",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                    callSite = new CallSiteInfo(word, 0);
            }
        }
        if (callSite == null)
            return null;

        var sig = ResolveProcedureSignature(source, callSite.ProcedureName);
        if (sig == null)
            return null;

        var paramLabels = sig.Parameters.Select(p => p.Label).ToArray();
        var sigInfo = new
        {
            label = $"{sig.Name}({string.Join(", ", sig.Parameters.Select(p => p.Name))})",
            documentation = sig.Kind switch
            {
                "extern" => $"extern procedure `{sig.Name}`.",
                _ => $"procedure `{sig.Name}`."
            },
            parameters = sig.Parameters.Select(p => new { label = p.Label, documentation = p.Documentation }).ToArray()
        };

        return new
        {
            signatures = new[] { sigInfo },
            activeSignature = 0,
            activeParameter = Math.Min(callSite.ActiveParameter, Math.Max(0, paramLabels.Length - 1))
        };
    }

    public static object[] GetDocumentHighlights(string source, int line, int character)
    {
        return FindSymbolOccurrences(source, line, character)
            .Select(r => new { range = r })
            .Cast<object>()
            .ToArray();
    }

    public static object[] GetReferences(string source, int line, int character, string uri)
    {
        return FindSymbolOccurrences(source, line, character)
            .Select(r => new { uri, range = r })
            .Cast<object>()
            .ToArray();
    }

    public static object GetSemanticTokens(string source)
    {
        var data = new List<int>();
        var lines = source.Replace("\r\n", "\n").Split('\n');
        var prevLine = 0;
        var prevChar = 0;

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            var line = lines[lineIdx];
            foreach (var token in TokenizeSemanticLine(line))
            {
                if (token.Length <= 0) continue;
                var deltaLine = lineIdx - prevLine;
                var deltaChar = deltaLine == 0 ? token.Start - prevChar : token.Start;
                data.Add(deltaLine);
                data.Add(deltaChar);
                data.Add(token.Length);
                data.Add(token.Type);
                data.Add(token.Modifiers);
                prevLine = lineIdx;
                prevChar = token.Start + token.Length;
            }
        }

        return new { data };
    }

    public static object GetSemanticTokensLegend() => new
    {
        tokenTypes = new[] { "keyword", "type", "register", "string", "number", "function", "variable", "deprecated" },
        tokenModifiers = new[] { "declaration", "readonly", "static", "deprecated" }
    };

    public static object? GetCodeActions(string source, int startLine, int startChar, int endLine, int endChar)
    {
        var edits = FormatDocument(source);
        if (edits == null)
            return new { actions = Array.Empty<object>() };

        return new
        {
            actions = new[]
            {
                new
                {
                    title = "Format document",
                    kind = "quickfix",
                    isPreferred = true,
                    edit = new
                    {
                        changes = new Dictionary<string, object?>
                        {
                            ["*"] = edits
                        }
                    }
                }
            }
        };
    }

    public static string? GetVirtualDocument(string kind, string source, string? filePath = null)
    {
        var path = filePath ?? "(virtual)";
        var result = new Compilation(path, source, CompilationOptions.Default).Process();
        if (!result.Success)
            return string.Join('\n', result.Diagnostics);

        var emitter = new NasmEmitter();
        var nasm = emitter.Emit(result.LoweredFunctions, result.StringLiterals, result.GlobalData);

        return kind.ToLowerInvariant() switch
        {
            "ir" => string.Join("\n\n", result.IrFunctions.Select(f => f.ToString())),
            "nasm" => nasm,
            "stack" => string.Join("\n\n", result.LoweredFunctions.Select(DescribeStackLayout)),
            _ => null
        };
    }

    private static readonly string[] CallerSavedRegisters =
    [
        "rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11"
    ];

    private static string DescribeStackLayout(LoweredFunction fn)
    {
        var lines = new List<string>
        {
            $"procedure {fn.Name}",
            "ABI: linux-x64-sysv (default)",
            $"  stack frame: {fn.StackFrameSize} bytes",
            $"  preserved (callee-saved): {(fn.PreservedRegisters.Count > 0 ? string.Join(", ", fn.PreservedRegisters) : "(none)")}",
            $"  caller-saved (clobbered across call): {string.Join(", ", CallerSavedRegisters)}"
        };
        foreach (var p in fn.Parameters)
            lines.Add($"  param[{p.Index}] {p.Name}");
        if (fn.RequiredExterns.Count > 0)
            lines.Add($"  externs: {string.Join(", ", fn.RequiredExterns)}");
        return string.Join('\n', lines);
    }

    public static object? ExecuteCommand(string command, string source, string? filePath)
    {
        var kind = command switch
        {
            "hla64.showIr" => "ir",
            "hla64.showNasm" => "nasm",
            "hla64.showStackLayout" => "stack",
            _ => null
        };
        if (kind == null) return null;
        var text = GetVirtualDocument(kind, source, filePath) ?? "";
        return new { title = $"HlaX64 {kind.ToUpperInvariant()}", content = text };
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

    private sealed record ProcedureSignature(string Name, string Kind, List<(string Name, string Label, string Documentation)> Parameters);

    private sealed record CallSiteInfo(string ProcedureName, int ActiveParameter);

    private sealed record SemanticTokenSpan(int Start, int Length, int Type, int Modifiers = 0);

    private static List<SymbolInfo> CollectSymbols(string source)
    {
        try
        {
            var program = new Parser(new Lexer(source).Tokenize()).Parse();
            var symbols = new List<SymbolInfo>();

            foreach (var ext in program.Externs.OfType<ExternProcedureNode>())
            {
                symbols.Add(new SymbolInfo(
                    ext.Name, "extern", ext.Line, ext.Column, ext.Line, ext.Column + ext.Name.Length, null));
                foreach (var param in ext.Parameters)
                {
                    symbols.Add(new SymbolInfo(
                        param.Name, "parameter", param.Line, param.Column,
                        param.Line, param.Column + param.Name.Length, ext.Name));
                }
            }

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

    private static ProcedureSignature? ResolveProcedureSignature(string source, string name)
    {
        try
        {
            var program = new Parser(new Lexer(source).Tokenize()).Parse();

            foreach (var ext in program.Externs.OfType<ExternProcedureNode>())
            {
                if (!ext.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                var ps = ext.Parameters.Select(p => (p.Name, $"{p.Name}: {p.Type}", $"extern parameter ({p.Type})")).ToList();
                return new ProcedureSignature(ext.Name, "extern", ps);
            }

            foreach (var proc in program.Statements.OfType<ProcedureNode>())
            {
                if (!proc.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    continue;
                var ps = proc.Parameters.Select(p => (p.Name, $"{p.Name}: {p.Type}", $"parameter ({p.Type})")).ToList();
                return new ProcedureSignature(proc.Name, "procedure", ps);
            }
        }
        catch (ParseException)
        {
            return ResolveProcedureSignatureFromText(source, name);
        }

        return ResolveProcedureSignatureFromText(source, name);
    }

    private static ProcedureSignature? ResolveProcedureSignatureFromText(string source, string name)
    {
        var pattern = $@"(?:extern\s+(?:variadic\s+)?procedure|procedure)\s+{System.Text.RegularExpressions.Regex.Escape(name)}\s*\(([^)]*)\)";
        var match = System.Text.RegularExpressions.Regex.Match(source, pattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline);
        if (!match.Success)
            return null;

        var paramText = match.Groups[1].Value.Trim();
        var ps = new List<(string Name, string Label, string Documentation)>();
        if (paramText.Length > 0)
        {
            foreach (var part in paramText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var bits = part.Split(':', 2, StringSplitOptions.TrimEntries);
                var pName = bits[0];
                var pType = bits.Length > 1 ? bits[1] : "int64";
                ps.Add((pName, $"{pName}: {pType}", $"parameter ({pType})"));
            }
        }

        var kind = match.Value.Contains("extern", StringComparison.OrdinalIgnoreCase) ? "extern" : "procedure";
        return new ProcedureSignature(name, kind, ps);
    }

    private static CallSiteInfo? FindCallSite(string source, int line, int character)
    {
        var lines = source.Replace("\r\n", "\n").Split('\n');
        if (line < 0 || line >= lines.Length)
            return null;

        var text = lines[line];
        if (character < 0) character = 0;
        if (character > text.Length) character = text.Length;

        var parenIdx = text.LastIndexOf('(', character - 1);
        if (parenIdx < 0)
            return null;

        var before = text[..parenIdx].TrimEnd();
        var match = System.Text.RegularExpressions.Regex.Match(before, @"(?:call\s+)?(\w+)\s*$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
            return null;

        var procName = match.Groups[1].Value;
        if (procName.Equals("call", StringComparison.OrdinalIgnoreCase))
            return null;

        var between = text.Substring(parenIdx + 1, Math.Max(0, character - parenIdx - 1));
        var active = between.Count(c => c == ',');
        return new CallSiteInfo(procName, active);
    }

    private static List<object> FindSymbolOccurrences(string source, int line, int character)
    {
        var word = GetWordAt(source, line, character);
        if (string.IsNullOrEmpty(word))
            return [];

        var symbols = CollectSymbols(source);
        var enclosing = FindEnclosingProcedure(symbols, line);
        var symbol = FindSymbol(symbols, word, enclosing);
        if (symbol == null && !symbols.Any(s => s.Name.Equals(word, StringComparison.OrdinalIgnoreCase)))
            return [];

        var scoped = symbol?.Kind is "variable" or "parameter";
        var container = scoped ? symbol!.ContainerProcedure : null;

        var results = new List<object>();
        var lines = source.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            var text = lines[i];
            var procAtLine = scoped ? FindEnclosingProcedure(symbols, i) : null;
            if (scoped && container != null && procAtLine != null &&
                !container.Equals(procAtLine, StringComparison.OrdinalIgnoreCase))
                continue;

            var col = 0;
            while (col < text.Length)
            {
                if (col + word.Length <= text.Length &&
                    text.AsSpan(col, word.Length).Equals(word, StringComparison.OrdinalIgnoreCase) &&
                    (col == 0 || !IsWordChar(text[col - 1])) &&
                    (col + word.Length >= text.Length || !IsWordChar(text[col + word.Length])))
                {
                    results.Add(ToRange(i, col, i, col + word.Length));
                }
                col++;
            }
        }

        return results;
    }

    private static IEnumerable<SemanticTokenSpan> TokenizeSemanticLine(string line)
    {
        int i = 0;
        while (i < line.Length)
        {
            if (char.IsWhiteSpace(line[i])) { i++; continue; }

            if (line[i] == '"')
            {
                int start = i;
                i++;
                while (i < line.Length && line[i] != '"')
                {
                    if (line[i] == '\\' && i + 1 < line.Length) i++;
                    i++;
                }
                if (i < line.Length) i++;
                yield return new SemanticTokenSpan(start, i - start, 3);
                continue;
            }

            if (char.IsDigit(line[i]) || (line[i] == '$' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
            {
                int start = i;
                if (line[i] == '$') i++;
                while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_')) i++;
                yield return new SemanticTokenSpan(start, i - start, 4);
                continue;
            }

            if (IsWordChar(line[i]))
            {
                int start = i;
                while (i < line.Length && IsWordChar(line[i])) i++;
                var word = line[start..i];
                int type = 6;
                int mods = 0;
                if (Keywords.Contains(word, StringComparer.OrdinalIgnoreCase))
                {
                    type = 0;
                    if (word.Equals("const", StringComparison.OrdinalIgnoreCase)) mods |= 1 << 1;
                    if (word.Equals("static", StringComparison.OrdinalIgnoreCase)) mods |= 1 << 2;
                }
                else if (Types.Contains(word, StringComparer.OrdinalIgnoreCase)) type = 1;
                else if (Registers.Contains(word, StringComparer.OrdinalIgnoreCase)) type = 2;
                else if (Mnemonics.Contains(word, StringComparer.OrdinalIgnoreCase)) type = 5;
                yield return new SemanticTokenSpan(start, i - start, type, mods);
                continue;
            }

            i++;
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
