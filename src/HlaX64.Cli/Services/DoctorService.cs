using HlaX64.Cli.Formatting;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Formatting;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Cli.Services;

public sealed class DoctorReport
{
    public bool Success { get; init; }
    public string Version { get; init; } = "";
    public List<DoctorCheck> Checks { get; init; } = [];

    public static DoctorReport Run()
    {
        var checks = new List<DoctorCheck>
        {
            DoctorChecks.CheckDotNet(),
            DoctorChecks.CheckPlatform(),
            DoctorChecks.CheckNasm(),
            DoctorChecks.CheckLinker(),
            DoctorChecks.CheckRuntimeFiles()
        };

        if (OperatingSystem.IsWindows())
            checks.Add(DoctorChecks.CheckWsl());

        return new DoctorReport
        {
            Success = checks.All(c => c.Passed),
            Version = Compilation.GetVersion(),
            Checks = checks
        };
    }
}

public sealed class DoctorCheck
{
    public string Name { get; init; } = "";
    public bool Passed { get; init; }
    public string Message { get; init; } = "";
}

internal static class DoctorChecks
{
    public static DoctorCheck CheckDotNet()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "--version",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });
            if (process == null) return Fail(".NET SDK", "Could not start dotnet process");
            process.WaitForExit(5000);
            return Pass(".NET SDK", process.StandardOutput.ReadToEnd().Trim());
        }
        catch (Exception ex)
        {
            return Fail(".NET SDK", ex.Message);
        }
    }

    public static DoctorCheck CheckPlatform() =>
        Pass("Platform", $"{Environment.OSVersion.Platform} {Environment.OSVersion.VersionString}");

    public static DoctorCheck CheckNasm()
    {
        if (NasmTool.TryFindNasm(out var path))
            return Pass("NASM", $"Found at {path}");
        return Fail("NASM", "Not found. Install from https://nasm.us");
    }

    public static DoctorCheck CheckLinker()
    {
        if (LinkerTool.TryFindLinker(out var path, out var display, out _))
            return Pass("Linker (Linux)", $"{display} at {path}");
        if (LinkerTool.TryFindWindowsLinker(out var winPath, out var winDisplay, out _))
            return Pass("Linker (Windows)", $"{winDisplay} at {winPath}");
        return Fail("Linker", "No supported linker found.");
    }

    public static DoctorCheck CheckWsl()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "--status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process == null) return Fail("WSL", "Not available");
            process.WaitForExit(5000);
            var output = process.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrEmpty(output))
                output = process.StandardError.ReadToEnd().Trim();
            return Pass("WSL", string.IsNullOrEmpty(output) ? "Available" : output.Split('\n')[0]);
        }
        catch
        {
            return Fail("WSL", "Not available");
        }
    }

    public static DoctorCheck CheckRuntimeFiles()
    {
        var runtimeDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "HlaX64.Runtime");
        var includeDir = Path.Combine(runtimeDir, "include");
        if (Directory.Exists(includeDir) && Directory.GetFiles(includeDir, "*.hhf").Length > 0)
            return Pass("Runtime files", $"Found in {Path.GetFullPath(runtimeDir)}");
        return Fail("Runtime files", $"Not found at {runtimeDir}");
    }

    private static DoctorCheck Pass(string name, string message) =>
        new() { Name = name, Passed = true, Message = message };

    private static DoctorCheck Fail(string name, string message) =>
        new() { Name = name, Passed = false, Message = message };
}

public static class FormatService
{
    public static (bool Changed, string Formatted) FormatFile(string path, bool write)
    {
        var original = File.ReadAllText(path);
        string formatted;
        try
        {
            formatted = AstFormatter.Format(original);
        }
        catch (ParseException)
        {
            formatted = SourceFormatter.Format(original);
        }

        var changed = original != formatted;
        if (changed && write)
            File.WriteAllText(path, formatted);
        return (changed, formatted);
    }
}
