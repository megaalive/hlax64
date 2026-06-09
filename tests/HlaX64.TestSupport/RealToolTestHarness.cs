using HlaX64.Cli.Toolchain;

namespace HlaX64.TestSupport;

public static class RealToolTestHarness
{
    public static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "HlaX64.slnx")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repo root");
    }

    public static string BuildWindowsArguments(string? argumentsPath, string repoRoot, string outputFile, int port = 0)
    {
        if (argumentsPath == null || !File.Exists(argumentsPath))
            return string.Empty;

        var raw = File.ReadAllText(argumentsPath);
        var tokens = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return string.Empty;

        var resolved = new List<string>();
        foreach (var token in tokens)
        {
            if (token == "$OUTPUT")
                resolved.Add(outputFile);
            else if (token == "$PORT")
                resolved.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else if (token == "$HOST")
                resolved.Add("127.0.0.1");
            else
                resolved.Add(ResolveRepoRelativeArgument(token, repoRoot));
        }

        return string.Join(' ', resolved.Select(arg => arg.Contains(' ') ? $"\"{arg}\"" : arg));
    }

    public static string BuildWslArguments(string? argumentsPath, string repoRoot, string outputFile, int port = 0, string? wslHostIp = null)
    {
        if (argumentsPath == null || !File.Exists(argumentsPath))
            return string.Empty;

        var hostIp = wslHostIp ?? WslHostResolver.TryGetHostIpForWsl() ?? "127.0.0.1";
        var raw = File.ReadAllText(argumentsPath);
        var tokens = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return string.Empty;

        var resolved = new List<string>();
        foreach (var token in tokens)
        {
            if (token == "$OUTPUT")
                resolved.Add(LinkerTool.ToWslPath(outputFile));
            else if (token == "$PORT")
                resolved.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else if (token == "$HOST" || token == "127.0.0.1")
                resolved.Add(hostIp);
            else
                resolved.Add(LinkerTool.ToWslPath(ResolveRepoRelativeArgument(token, repoRoot)));
        }

        return string.Join(' ', resolved.Select(arg => $"'{arg.Replace("'", "'\\''")}'"));
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
}
