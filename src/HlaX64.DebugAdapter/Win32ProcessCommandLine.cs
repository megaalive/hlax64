using System.Text;

namespace HlaX64.DebugAdapter;

public static class Win32ProcessCommandLine
{
    public static string BuildCreateProcessCommandLine(string executablePath, IReadOnlyList<string>? arguments)
    {
        var sb = new StringBuilder();
        sb.Append('"').Append(executablePath).Append('"');
        if (arguments == null)
            return sb.ToString();

        foreach (var argument in arguments)
        {
            if (string.IsNullOrEmpty(argument))
                continue;

            sb.Append(' ');
            sb.Append(QuoteArgument(argument));
        }

        return sb.ToString();
    }

    public static string FormatArgsForLog(IReadOnlyList<string>? arguments)
    {
        if (arguments == null || arguments.Count == 0)
            return "(no args)";

        return string.Join(' ', arguments);
    }

    private static string QuoteArgument(string argument)
    {
        if (!argument.Any(c => char.IsWhiteSpace(c) || c == '"'))
            return argument;

        return "\"" + argument.Replace("\"", "\\\"") + "\"";
    }
}
