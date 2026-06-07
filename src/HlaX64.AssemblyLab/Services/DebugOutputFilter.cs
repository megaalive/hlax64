using System.Text.RegularExpressions;

namespace HlaX64.AssemblyLab.Services;

internal static partial class DebugOutputFilter
{
    public static bool ShouldShow(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return false;

        if (line.StartsWith("← ", StringComparison.Ordinal)
            || line.StartsWith("Debug:", StringComparison.Ordinal)
            || line.StartsWith("DAP:", StringComparison.Ordinal)
            || line.StartsWith("  rip=", StringComparison.Ordinal)
            || line.StartsWith("  rax=", StringComparison.Ordinal))
            return true;

        if (line.StartsWith("> ", StringComparison.Ordinal))
            return !TokenResponse().IsMatch(line[2..]);

        if (line.Contains("^error", StringComparison.OrdinalIgnoreCase))
        {
            if (line.Contains("data-evaluate-expression", StringComparison.OrdinalIgnoreCase)
                && line.Contains("undefined-command", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        if (line is "(gdb)")
            return false;

        if (line.StartsWith('^') || line.StartsWith('=') || line.StartsWith('*'))
            return false;

        if (line.StartsWith("~\"", StringComparison.Ordinal))
            return line.Contains("Cannot insert", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("Cannot access memory", StringComparison.OrdinalIgnoreCase);

        if (line.StartsWith("&\"", StringComparison.Ordinal))
            return line.Contains("Cannot insert", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("Cannot access memory", StringComparison.OrdinalIgnoreCase)
                   || line.Contains("Warning", StringComparison.OrdinalIgnoreCase);

        return false;
    }

    public static string FormatCommand(string cmd) =>
        cmd.StartsWith('-') ? $"> {cmd}" : $"> {cmd}";

    [GeneratedRegex(@"^\d+-")]
    private static partial Regex TokenResponse();
}
