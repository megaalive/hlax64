namespace HlaX64.Cli.Formatting;

/// <summary>
/// Normalizes HlaX64 source layout: indentation, semicolons, trailing whitespace.
/// </summary>
public static class SourceFormatter
{
    private static readonly HashSet<string> BlockOpeners = new(StringComparer.OrdinalIgnoreCase)
    {
        "begin", "then", "do"
    };

    private static readonly HashSet<string> BlockClosers = new(StringComparer.OrdinalIgnoreCase)
    {
        "end", "else", "endif", "endwhile"
    };

    public static string Format(string source)
    {
        var lines = source.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var output = new List<string>();
        var indent = 0;

        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0)
            {
                output.Add("");
                continue;
            }

            if (StartsWithKeyword(trimmed, "else", "endif", "endwhile") ||
                StartsWithKeyword(trimmed, "end "))
            {
                indent = Math.Max(0, indent - 1);
            }

            var formatted = FormatLine(trimmed, indent);
            output.Add(formatted);

            if (ContainsBlockOpener(trimmed))
                indent++;
        }

        return string.Join(Environment.NewLine, output).TrimEnd() + Environment.NewLine;
    }

    private static string FormatLine(string line, int indent)
    {
        if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("#include") ||
            line.StartsWith("#pragma") || line.StartsWith("program ") ||
            line.StartsWith("procedure ") || line.StartsWith("export procedure "))
        {
            return new string(' ', indent * 4) + EnsureSemicolon(line);
        }

        return new string(' ', indent * 4) + EnsureSemicolon(line);
    }

    private static string EnsureSemicolon(string line)
    {
        if (line.EndsWith(';') || line.EndsWith('{') || line.EndsWith('}'))
            return line;

        if (line.StartsWith("begin ") || line.StartsWith("end ") ||
            line.Equals("else", StringComparison.OrdinalIgnoreCase) ||
            line.StartsWith("else ") ||
            line.Equals("endif", StringComparison.OrdinalIgnoreCase) ||
            line.Equals("endwhile", StringComparison.OrdinalIgnoreCase) ||
            line.Contains(" then") || line.Contains(" do"))
            return line;

        return line + ";";
    }

    private static bool ContainsBlockOpener(string line)
    {
        var lower = line.ToLowerInvariant();
        if (lower.StartsWith("begin "))
            return true;
        if (lower.Contains(" then"))
            return true;
        if (lower.StartsWith("while(") && lower.Contains(" do"))
            return true;
        return false;
    }

    private static bool StartsWithKeyword(string line, params string[] keywords)
    {
        foreach (var kw in keywords)
        {
            if (line.Equals(kw, StringComparison.OrdinalIgnoreCase))
                return true;
            if (line.StartsWith(kw + " ", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
