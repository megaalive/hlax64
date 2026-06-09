namespace HlaX64.Cli.Toolchain;

public enum ToolchainSource
{
    UserSetting,
    Bundled,
    Environment,
    Path,
    CommonLocation,
    Wsl,
    Missing
}

public sealed record ToolchainResolution(
    string Name,
    bool Found,
    string? Path,
    ToolchainSource Source,
    string Message,
    string? InstallHint = null);

public sealed class ToolchainResolver
{
    public const string InstallGuideRelative = "docs/install.md";

    private readonly ToolchainSettings _settings;
    private readonly string _baseDirectory;

    public ToolchainResolver(ToolchainSettings? settings = null, string? baseDirectory = null)
    {
        _settings = settings ?? ToolchainSettings.Load();
        _baseDirectory = baseDirectory ?? AppContext.BaseDirectory;
    }

    public static ToolchainResolver Default { get; } = new();

    public ToolchainResolution ResolveHla64()
    {
        if (TryExistingFile(_settings.Hla64Path, out var user))
            return Found("HlaX64 CLI", user, ToolchainSource.UserSetting);

        foreach (var name in OperatingSystem.IsWindows()
                     ? new[] { "HlaX64.Cli.exe", "hla64.cmd" }
                     : new[] { "HlaX64.Cli", "hla64.sh" })
        {
            var candidate = Path.Combine(_baseDirectory, name);
            if (File.Exists(candidate))
                return Found("HlaX64 CLI", candidate, ToolchainSource.Bundled);
        }

        if (TryExistingFile(Environment.GetEnvironmentVariable("HLA64"), out var env))
            return Found("HlaX64 CLI", env, ToolchainSource.Environment);

        if (TryFindOnPath("hla64", out var path))
            return Found("HlaX64 CLI", path, ToolchainSource.Path);

        return Missing("HlaX64 CLI", "Bundled hla64 was not found.");
    }

    public ToolchainResolution ResolveRuntimeDirectory()
    {
        if (TryExistingDirectory(_settings.RuntimeDirectory, out var user))
            return Found("Runtime files", user, ToolchainSource.UserSetting);

        if (TryExistingDirectory(Environment.GetEnvironmentVariable("HLAX64_RUNTIME_DIR"), out var env))
            return Found("Runtime files", env, ToolchainSource.Environment);

        foreach (var candidate in EnumerateRuntimeCandidates())
        {
            if (IsRuntimeDirectory(candidate))
                return Found("Runtime files", candidate, ToolchainSource.Bundled);
        }

        return Missing("Runtime files", "Runtime NASM files not found. Set HLAX64_RUNTIME_DIR or reinstall Assembly Lab.");
    }

    public ToolchainResolution ResolveNasm()
    {
        if (TryExistingFile(_settings.NasmPath, out var user))
            return Found("NASM", user, ToolchainSource.UserSetting);

        if (TryExistingFile(Environment.GetEnvironmentVariable("NASM"), out var env))
            return Found("NASM", env, ToolchainSource.Environment);

        foreach (var name in OperatingSystem.IsWindows() ? new[] { "nasm.exe" } : new[] { "nasm" })
        {
            var candidate = Path.Combine(_baseDirectory, "tools", name);
            if (File.Exists(candidate))
                return Found("NASM", candidate, ToolchainSource.Bundled);
        }

        foreach (var candidate in EnumerateBundledToolCandidates("nasm"))
        {
            if (File.Exists(candidate))
                return Found("NASM", candidate, ToolchainSource.Bundled);
        }

        if (TryFindOnPath("nasm", out var path))
            return Found("NASM", path, ToolchainSource.Path);

        if (OperatingSystem.IsWindows() && TryRunProbe("wsl", "nasm --version"))
            return Found("NASM", "wsl|nasm", ToolchainSource.Wsl);

        return Missing("NASM", "NASM not found. Install NASM or set NASM path in Assembly Lab settings.",
            FormatInstallHint(OperatingSystem.IsWindows() ? "winget install -e --id NASM.NASM" : "sudo apt install nasm"));
    }

    public ToolchainResolution ResolveWindowsLinker()
    {
        if (TryExistingFile(_settings.WindowsLinkerPath, out var user))
            return Found("Windows linker", user, ToolchainSource.UserSetting);

        if (TryExistingFile(Environment.GetEnvironmentVariable("LLD_LINK"), out var env))
            return Found("Windows linker", env, ToolchainSource.Environment);

        foreach (var name in new[] { "lld-link.exe", "link.exe" })
        {
            var candidate = Path.Combine(_baseDirectory, "tools", name);
            if (File.Exists(candidate))
                return Found("Windows linker", candidate, ToolchainSource.Bundled);
        }

        foreach (var name in new[] { "lld-link", "link", "link.exe" })
        {
            if (TryFindOnPath(name, out var path))
                return Found("Windows linker", path, ToolchainSource.Path);
        }

        foreach (var dir in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin"),
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LLVM", "bin")
                 })
        {
            var candidate = Path.Combine(dir, "lld-link.exe");
            if (File.Exists(candidate))
                return Found("Windows linker", candidate, ToolchainSource.CommonLocation);
        }

        return Missing("Windows linker", "No lld-link or MSVC link.exe found.",
            FormatInstallHint("winget install -e --id LLVM.LLVM"));
    }

    public ToolchainResolution ResolveLinuxLinker()
    {
        if (TryExistingFile(_settings.LinuxLinkerPath, out var user))
            return Found("Linux linker", user, ToolchainSource.UserSetting);

        if (TryExistingFile(Environment.GetEnvironmentVariable("HLAX64_LINUX_LINKER"), out var env))
            return Found("Linux linker", env, ToolchainSource.Environment);

        foreach (var name in new[] { "gcc", "ld" })
        {
            var candidate = Path.Combine(_baseDirectory, "tools", name);
            if (File.Exists(candidate))
                return Found("Linux linker", candidate, ToolchainSource.Bundled);

            if (TryFindOnPath(name, out var path))
                return Found("Linux linker", path, ToolchainSource.Path);
        }

        if (OperatingSystem.IsWindows() && TryRunProbe("wsl", "gcc --version"))
            return Found("Linux linker", "wsl|gcc", ToolchainSource.Wsl);

        return Missing("Linux linker", "No gcc/ld linker found.",
            FormatInstallHint(OperatingSystem.IsWindows()
                ? "wsl --install -d Ubuntu"
                : "sudo apt install build-essential"));
    }

    public IReadOnlyList<ToolchainResolution> ResolveAll() =>
    [
        ResolveHla64(),
        ResolveRuntimeDirectory(),
        ResolveNasm(),
        ResolveWindowsLinker(),
        ResolveLinuxLinker()
    ];

    public void ApplyToEnvironment()
    {
        SetIfFound("HLA64", ResolveHla64());
        SetIfFound("HLAX64_RUNTIME_DIR", ResolveRuntimeDirectory());
        SetIfFound("NASM", ResolveNasm());
        SetIfFound("LLD_LINK", ResolveWindowsLinker());
        SetIfFound("HLAX64_LINUX_LINKER", ResolveLinuxLinker());
    }

    private static void SetIfFound(string variable, ToolchainResolution resolution)
    {
        if (resolution.Found && !string.IsNullOrWhiteSpace(resolution.Path))
            Environment.SetEnvironmentVariable(variable, resolution.Path);
    }

    private IEnumerable<string> EnumerateRuntimeCandidates()
    {
        yield return Path.Combine(_baseDirectory, "runtime");
        yield return Path.Combine(_baseDirectory, "HlaX64.Runtime");
        yield return Path.Combine(_baseDirectory, "src", "HlaX64.Runtime");

        var dir = _baseDirectory;
        for (var i = 0; i < 8; i++)
        {
            yield return Path.Combine(dir, "runtime");
            yield return Path.Combine(dir, "src", "HlaX64.Runtime");
            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;
            dir = parent.FullName;
        }
    }

    private IEnumerable<string> EnumerateBundledToolCandidates(string toolName)
    {
        var names = OperatingSystem.IsWindows() ? new[] { toolName + ".exe", toolName } : new[] { toolName };
        var dir = _baseDirectory;
        for (var i = 0; i < 8; i++)
        {
            foreach (var name in names)
            {
                yield return Path.Combine(dir, "tools", name);
                yield return Path.Combine(dir, "third_party", "tools", name);
            }

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;
            dir = parent.FullName;
        }
    }

    private static bool IsRuntimeDirectory(string path) =>
        Directory.Exists(path)
        && (File.Exists(Path.Combine(path, "linux-x64", "stdout.nasm"))
            || File.Exists(Path.Combine(path, "windows-x64", "stdout.nasm")));

    private static ToolchainResolution Found(string name, string path, ToolchainSource source) =>
        new(name, true, path, source, $"Found at {path}");

    private static ToolchainResolution Missing(string name, string message, string? hint = null) =>
        new(name, false, null, ToolchainSource.Missing, message, hint);

    private static string FormatInstallHint(string command)
        => $"{command} — see {InstallGuideRelative}";

    private static bool TryExistingFile(string? path, out string resolved)
    {
        resolved = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;
        if (!File.Exists(path))
            return false;
        resolved = Path.GetFullPath(path);
        return true;
    }

    private static bool TryExistingDirectory(string? path, out string resolved)
    {
        resolved = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;
        if (!Directory.Exists(path))
            return false;
        resolved = Path.GetFullPath(path);
        return true;
    }

    private static bool TryFindOnPath(string command, out string resolved)
    {
        resolved = "";
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var extensions = OperatingSystem.IsWindows()
            ? (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [""];

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(dir, command.EndsWith(ext, StringComparison.OrdinalIgnoreCase) ? command : command + ext);
                if (File.Exists(candidate))
                {
                    resolved = candidate;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryRunProbe(string fileName, string arguments)
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null)
                return false;
            process.WaitForExit(3000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
