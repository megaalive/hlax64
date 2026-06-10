namespace HlaX64.DebugAdapter;

public static class NasmLineClassifier
{
    public static bool IsInstructionLine(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(';'))
            return false;

        if (IsDirective(trimmed) || IsLabelOnly(trimmed))
            return false;

        return true;
    }

    public static bool IsLabelOnly(string line)
    {
        var trimmed = line.Trim();
        var colon = trimmed.IndexOf(':');
        if (colon < 0)
            return false;

        return trimmed[(colon + 1)..].Trim().Length == 0;
    }

    internal static bool IsDirective(string line)
    {
        if (line.EndsWith(':'))
            return false;

        var first = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return first.Equals("bits", StringComparison.OrdinalIgnoreCase)
               || first.Equals("default", StringComparison.OrdinalIgnoreCase)
               || first.Equals("section", StringComparison.OrdinalIgnoreCase)
               || first.Equals("global", StringComparison.OrdinalIgnoreCase)
               || first.Equals("extern", StringComparison.OrdinalIgnoreCase);
    }
}
