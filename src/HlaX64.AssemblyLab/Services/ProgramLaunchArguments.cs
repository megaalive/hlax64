using System.Text;

namespace HlaX64.AssemblyLab.Services;

public static class ProgramLaunchArguments
{
    public static bool ShouldPrompt(string sourceText, string sourcePath, string projectFolder)
    {
        if (TryFindExpectedArgumentsPath(sourcePath, projectFolder) != null)
            return true;

        return SourceUsesCommandLineArguments(sourceText);
    }

    public static bool SourceUsesCommandLineArguments(string sourceText)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
            return false;

        return sourceText.Contains("hlax_argv_get", StringComparison.Ordinal)
               || sourceText.Contains("hlax_argv_count", StringComparison.Ordinal);
    }

    public static string? TryFindExpectedArgumentsPath(string sourcePath, string projectFolder)
    {
        foreach (var dir in CandidateDirectories(sourcePath, projectFolder))
        {
            var path = Path.Combine(dir, "expected.arguments");
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static string GetDefaultArgumentsText(string sourcePath, string projectFolder, string repoRoot)
    {
        var argumentsPath = TryFindExpectedArgumentsPath(sourcePath, projectFolder);
        if (argumentsPath == null)
            return string.Empty;

        return ResolveExpectedArgumentsText(argumentsPath, repoRoot);
    }

    public static string GetPromptHint(string sourceText)
    {
        if (sourceText.Contains("hlax_argv_get", StringComparison.Ordinal))
            return "This program reads command-line arguments (argv). Enter one token per line or space-separated.";

        return "This program checks argv count. Enter arguments for the happy path, or leave empty to test usage/error paths.";
    }

    public static string[] Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return [];

        return SplitCommandLine(input);
    }

    public static string ResolveExpectedArgumentsText(string argumentsPath, string repoRoot)
    {
        var raw = File.ReadAllText(argumentsPath);
        var tokens = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return string.Empty;

        var resolved = new List<string>();
        foreach (var token in tokens)
        {
            resolved.Add(token switch
            {
                "$OUTPUT" => Path.Combine(Path.GetTempPath(), "hlax64-debug-output.txt"),
                "$PORT" => "5010",
                "$HOST" => "127.0.0.1",
                _ => ResolveRepoRelativeArgument(token, repoRoot)
            });
        }

        return string.Join(' ', resolved);
    }

    private static IEnumerable<string> CandidateDirectories(string sourcePath, string projectFolder)
    {
        if (!string.IsNullOrWhiteSpace(projectFolder) && Directory.Exists(projectFolder))
            yield return projectFolder;

        if (!string.IsNullOrWhiteSpace(sourcePath) && sourcePath != "(unsaved)")
        {
            var sourceDir = Path.GetDirectoryName(Path.GetFullPath(sourcePath));
            if (!string.IsNullOrWhiteSpace(sourceDir))
                yield return sourceDir;
        }
    }

    private static string ResolveRepoRelativeArgument(string token, string repoRoot)
    {
        if (Path.IsPathRooted(token))
            return token;

        var candidate = Path.Combine(repoRoot, token);
        if (File.Exists(candidate) || Directory.Exists(candidate))
            return candidate;

        return token;
    }

    private static string[] SplitCommandLine(string input)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < input.Length && input[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(ch))
            {
                if (current.Length > 0)
                {
                    args.Add(current.ToString());
                    current.Clear();
                }

                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
            args.Add(current.ToString());

        return args.ToArray();
    }
}
