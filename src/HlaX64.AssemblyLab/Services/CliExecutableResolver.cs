using System.Text;
using System.Diagnostics;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Resolves <c>hla64</c> invocations and generic shell commands for the Terminal panel.</summary>
public static class CliExecutableResolver
{
    public static bool TryCreateProcessStartInfo(
        string commandLine,
        string? workingDirectory,
        string? repoRoot,
        out ProcessStartInfo startInfo,
        out string? error)
    {
        startInfo = null!;
        error = null;

        var trimmed = commandLine.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            error = "Enter a command.";
            return false;
        }

        repoRoot ??= FindRepoRoot();
        workingDirectory = ResolveWorkingDirectory(workingDirectory, repoRoot);

        if (TryStripCliPrefix(trimmed, out var cliArgs))
        {
            if (!TryResolveHla64(cliArgs, repoRoot, out var fileName, out var arguments))
            {
                error = "hla64 CLI not found (build solution or run Assembly Lab from publish output).";
                return false;
            }

            startInfo = CreateRedirectedStartInfo(fileName, arguments, workingDirectory);
            return true;
        }

        startInfo = CreateShellWrappedStartInfo(trimmed, workingDirectory);
        return true;
    }

    public static string FormatHla64Command(string subcommand, string sourcePath, string target)
    {
        var quoted = QuoteIfNeeded(sourcePath);
        return $"hla64 {subcommand} {quoted} --target {target}";
    }

    /// <summary>Rewrites <c>hla64 …</c> to a fully resolved CLI invocation for the interactive terminal.</summary>
    public static bool TryResolveTerminalCommand(string commandLine, string? repoRoot, out string resolved)
    {
        resolved = commandLine;
        var trimmed = commandLine.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return false;

        if (!TryStripCliPrefix(trimmed, out var cliArgs))
            return false;

        repoRoot ??= FindRepoRoot();
        if (!TryResolveHla64(cliArgs, repoRoot, out var fileName, out var arguments))
            return false;

        resolved = $"{QuoteIfNeeded(fileName)} {arguments}";
        return true;
    }

    public static bool TryFormatResolvedCommand(
        string subcommand,
        string? sourcePath,
        string? target,
        string? repoRoot,
        out string commandLine)
    {
        commandLine = "";
        repoRoot ??= FindRepoRoot();

        var args = new StringBuilder(subcommand);
        if (!string.IsNullOrWhiteSpace(sourcePath))
            args.Append(' ').Append(QuoteIfNeeded(sourcePath));
        if (!string.IsNullOrWhiteSpace(target))
            args.Append(" --target ").Append(target);

        if (!TryResolveHla64(args.ToString(), repoRoot, out var fileName, out var arguments))
            return false;

        commandLine = $"{QuoteIfNeeded(fileName)} {arguments}";
        return true;
    }

    private static bool TryStripCliPrefix(string commandLine, out string cliArgs)
    {
        cliArgs = "";
        if (commandLine.Equals("hla64", StringComparison.OrdinalIgnoreCase) ||
            commandLine.Equals("hlax64", StringComparison.OrdinalIgnoreCase))
            return true;

        if (commandLine.StartsWith("hla64 ", StringComparison.OrdinalIgnoreCase))
        {
            cliArgs = commandLine[6..].TrimStart();
            return true;
        }

        if (commandLine.StartsWith("hlax64 ", StringComparison.OrdinalIgnoreCase))
        {
            cliArgs = commandLine[7..].TrimStart();
            return true;
        }

        return false;
    }

    private static bool TryResolveHla64(string cliArgs, string repoRoot, out string fileName, out string arguments)
    {
        fileName = "";
        arguments = cliArgs;

        var baseDir = AppContext.BaseDirectory;
        foreach (var name in new[] { "HlaX64.Cli.exe", "HlaX64.Cli" })
        {
            var exe = Path.Combine(baseDir, name);
            if (File.Exists(exe))
            {
                fileName = exe;
                return true;
            }
        }

        foreach (var config in new[] { "Debug", "Release" })
        {
            var dll = Path.Combine(repoRoot, "src", "HlaX64.Cli", "bin", config, "net10.0", "HlaX64.Cli.dll");
            if (!File.Exists(dll))
                continue;

            fileName = "dotnet";
            arguments = $"exec \"{dll}\" {cliArgs}";
            return true;
        }

        var project = Path.Combine(repoRoot, "src", "HlaX64.Cli", "HlaX64.Cli.csproj");
        if (File.Exists(project))
        {
            fileName = "dotnet";
            arguments = $"run --project \"{project}\" -- {cliArgs}";
            return true;
        }

        return false;
    }

    private static ProcessStartInfo CreateShellWrappedStartInfo(string commandLine, string workingDirectory)
    {
        if (OperatingSystem.IsWindows())
            return CreateRedirectedStartInfo("cmd.exe", $"/d /s /c \"{commandLine.Replace("\"", "\\\"")}\"", workingDirectory);

        return CreateRedirectedStartInfo("/bin/sh", $"-c \"{commandLine.Replace("\"", "\\\"")}\"", workingDirectory);
    }

    private static ProcessStartInfo CreateRedirectedStartInfo(string fileName, string arguments, string workingDirectory)
    {
        return new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
    }

    private static string ResolveWorkingDirectory(string? workingDirectory, string repoRoot)
    {
        if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            return workingDirectory;

        if (Directory.Exists(repoRoot))
            return repoRoot;

        return Directory.GetCurrentDirectory();
    }

    private static string QuoteIfNeeded(string path)
    {
        if (path.Contains(' ') || path.Contains('\t'))
            return $"\"{path}\"";
        return path;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "HlaX64.slnx")))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;
            dir = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
