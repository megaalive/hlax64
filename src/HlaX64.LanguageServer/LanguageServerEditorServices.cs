namespace HlaX64.LanguageServer;

public static class LanguageServerEditorServices
{
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

        return new { isIncomplete = false, items };
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
