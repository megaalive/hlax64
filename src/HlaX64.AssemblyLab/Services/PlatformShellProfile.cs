using System.Runtime.InteropServices;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Platform-native interactive shell selection for the Assembly Lab terminal.</summary>
public static class PlatformShellProfile
{
    public sealed record ShellSpec(string Executable, string[] Arguments, string DisplayName);

    public static ShellSpec Resolve()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return ResolveWindows();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return ResolveUnix(["zsh", "bash", "sh"]);

        return ResolveUnix(["bash", "ash", "sh"]);
    }

    public static Dictionary<string, string> BuildEnvironment(string workingDirectory, string? repoRoot)
    {
        repoRoot ??= FindRepoRoot();
        var env = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string key && entry.Value is string value)
                env[key] = value;
        }

        env["TERM"] = "xterm-256color";
        env["COLORTERM"] = "truecolor";

        if (TryResolveCliExecutable(repoRoot, out var cliPath))
        {
            env["HLA64"] = cliPath;
            PrependPath(env, Path.GetDirectoryName(cliPath)!);
        }

        env["HLAX64_REPO"] = repoRoot;
        env["HLAX64_LAB"] = "1";

        var baseDir = AppContext.BaseDirectory;
        PrependPath(env, baseDir);

        var cliDir = Path.Combine(baseDir, "cli");
        PrependPath(env, cliDir);

        if (Directory.Exists(workingDirectory))
            env["PWD"] = workingDirectory;

        return env;
    }

    public static string BuildWelcomeBanner(string workingDirectory, string? repoRoot)
    {
        repoRoot ??= FindRepoRoot();
        var shell = Resolve();
        var cliHint = TryResolveCliExecutable(repoRoot, out var cli)
            ? cli
            : "hla64 (build solution first)";

        return $"""
            HlaX64 Assembly Lab — interactive shell ({shell.DisplayName})
            cwd: {workingDirectory}
            CLI: {cliHint}

            Examples:
              hla64 doctor
              hla64 build "file.hla64" --target windows-x64-msabi
              hla64 run "file.hla64" --target windows-x64-msabi

            Use toolbar Build / Run / Doctor to inject commands.

            """;
    }

    private static ShellSpec ResolveWindows()
    {
        if (FindOnPath("pwsh.exe") is not null)
            return new ShellSpec("pwsh.exe", ["-NoLogo"], "PowerShell 7");

        if (FindOnPath("powershell.exe") is not null)
            return new ShellSpec("powershell.exe", ["-NoLogo"], "Windows PowerShell");

        var comSpec = Environment.GetEnvironmentVariable("ComSpec");
        if (!string.IsNullOrWhiteSpace(comSpec) && File.Exists(comSpec))
            return new ShellSpec(comSpec, [], Path.GetFileName(comSpec));

        return new ShellSpec("cmd.exe", [], "cmd.exe");
    }

    private static ShellSpec ResolveUnix(string[] candidates)
    {
        foreach (var name in candidates)
        {
            if (FindOnPath(name) is not null)
                return new ShellSpec(name, ["-i"], name);
        }

        return new ShellSpec("/bin/sh", ["-i"], "sh");
    }

    private static string? FindOnPath(string command)
    {
        if (Path.IsPathRooted(command) && File.Exists(command))
            return command;

        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return null;

        IEnumerable<string> extensions = [string.Empty];
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
            if (!string.IsNullOrWhiteSpace(pathExt))
                extensions = pathExt.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    private static bool TryResolveCliExecutable(string repoRoot, out string cliPath)
    {
        cliPath = "";
        var baseDir = AppContext.BaseDirectory;

        foreach (var name in new[] { "HlaX64.Cli.exe", "HlaX64.Cli" })
        {
            var exe = Path.Combine(baseDir, name);
            if (File.Exists(exe))
            {
                cliPath = exe;
                return true;
            }
        }

        foreach (var config in new[] { "Debug", "Release" })
        {
            var dll = Path.Combine(repoRoot, "src", "HlaX64.Cli", "bin", config, "net10.0", "HlaX64.Cli.dll");
            if (File.Exists(dll))
            {
                cliPath = dll;
                return true;
            }
        }

        return false;
    }

    private static void PrependPath(Dictionary<string, string> env, string directory)
    {
        if (!Directory.Exists(directory))
            return;

        env.TryGetValue("PATH", out var current);
        env["PATH"] = string.IsNullOrEmpty(current)
            ? directory
            : directory + Path.PathSeparator + current;
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
