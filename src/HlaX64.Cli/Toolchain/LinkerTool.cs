using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HlaX64.Cli.Toolchain;

/// <summary>
/// Detects and invokes the system linker for Linux x64 (ELF).
/// Supports: native Linux (gcc/ld), WSL (wsl gcc), MinGW-w64 cross-compiler.
/// </summary>
public sealed class LinkerTool
{
    private static readonly LinkerInfo[] LinkerCandidates =
    {
        // WSL (Windows Subsystem for Linux) - preferred on Windows for Linux targets
        new("wsl", "gcc", "--version", "WSL (Ubuntu/Debian)", "Install: wsl --install -d Ubuntu && sudo apt install gcc"),
        new("wsl", "ld", "--version", "WSL ld", ""),
        
        // MinGW-w64 cross-compiler (Windows native, produces PE/COFF but can target ELF with -f elf64)
        new("x86_64-w64-mingw32-gcc", "", "--version", "MinGW-w64 GCC", "Install: winget install -e --id mingw-w64.mingw-w64"),
        new("x86_64-w64-mingw32-ld", "", "--version", "MinGW-w64 ld", ""),
        new("gcc", "", "--version", "MinGW GCC (in PATH)", "Install: choco install mingw-w64"),
        
        // Native Linux
        new("gcc", "", "--version", "Native Linux GCC", ""),
        new("ld", "", "--version", "Native Linux ld", ""),
        new("/usr/bin/gcc", "", "--version", "Native Linux GCC", ""),
        new("/usr/bin/ld", "", "--version", "Native Linux ld", ""),
    };

    private static readonly LinkerInfo[] WindowsLinkerCandidates =
    {
        // lld-link (LLVM COFF linker) - preferred on Windows for COFF targets
        new("lld-link", "", "--version", "LLD (COFF)", "Install: winget install -e --id LLVM.LLVM or choco install llvm"),
        // MSVC link.exe
        new("link", "", "--version", "MSVC link.exe", "Install: Visual Studio Build Tools (https://visualstudio.microsoft.com/downloads/)"),
        new("link.exe", "", "--version", "MSVC link.exe", ""),
    };

    private sealed record LinkerInfo(
        string Command,
        string SubCommand,
        string VersionArg,
        string DisplayName,
        string InstallHint);

    private static readonly string[] CommonWindowsToolDirectories =
    [
        @"C:\Program Files\LLVM\bin",
        @"C:\Program Files (x86)\LLVM\bin",
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "LLVM", "bin"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "LLVM", "bin"),
    ];

    /// <summary>
    /// Resolves a tool executable from PATH or common install locations (e.g. LLVM not added to PATH).
    /// </summary>
    public static string? ResolveToolExecutable(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
            return null;

        if (Path.IsPathRooted(toolName) && File.Exists(toolName))
            return toolName;

        foreach (var dir in EnumerateToolSearchDirectories())
        {
            var candidate = Path.Combine(dir, toolName);
            if (File.Exists(candidate))
                return candidate;

            if (OperatingSystem.IsWindows())
            {
                var withExe = candidate + ".exe";
                if (File.Exists(withExe))
                    return withExe;
            }
        }

        return null;
    }

    public static bool IsWslAvailable()
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "--status",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            if (process == null)
                return false;
            process.WaitForExit(5000);
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static IEnumerable<string> EnumerateToolSearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim().Trim('"');
            if (!string.IsNullOrWhiteSpace(trimmed) && seen.Add(trimmed))
                yield return trimmed;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var dir in CommonWindowsToolDirectories)
            {
                if (!string.IsNullOrWhiteSpace(dir) && seen.Add(dir))
                    yield return dir;
            }
        }
    }

    private static bool TryRunVersionProbe(string fileName, string arguments, out string resolvedPath)
    {
        resolvedPath = fileName;
        foreach (var candidatePath in EnumerateCandidateExecutables(fileName))
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidatePath,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null)
                    continue;

                process.WaitForExit(3000);
                if (process.ExitCode == 0)
                {
                    resolvedPath = candidatePath;
                    return true;
                }
            }
            catch
            {
                // Try next candidate
            }
        }

        return false;
    }

    private static IEnumerable<string> EnumerateCandidateExecutables(string toolName)
    {
        if (Path.IsPathRooted(toolName))
        {
            yield return toolName;
            yield break;
        }

        yield return toolName;

        var resolved = ResolveToolExecutable(toolName);
        if (resolved != null && !string.Equals(resolved, toolName, StringComparison.OrdinalIgnoreCase))
            yield return resolved;
    }

    /// <summary>
    /// Tries to find a working linker that can produce Linux x64 ELF executables.
    /// </summary>
    public static bool TryFindLinker(out string path, out string displayName, out string versionArgs)
    {
        foreach (var candidate in LinkerCandidates)
        {
            string args = string.IsNullOrEmpty(candidate.SubCommand)
                ? candidate.VersionArg
                : $"{candidate.SubCommand} {candidate.VersionArg}";

            if (TryRunVersionProbe(candidate.Command, args, out var resolvedPath))
            {
                path = resolvedPath;
                displayName = candidate.DisplayName;
                versionArgs = candidate.SubCommand;
                return true;
            }
        }

        path = string.Empty;
        displayName = string.Empty;
        versionArgs = string.Empty;
        return false;
    }

    /// <summary>
    /// Links an object file into an executable or shared library using the detected linker.
    /// Returns true if linking succeeded. Sets requiresWslRun if the executable must be run via WSL.
    /// </summary>
    public static bool TryLink(string objectFile, string output, out string error, out bool requiresWslRun,
        bool shared = false, IEnumerable<string>? extraLibraries = null)
    {
        requiresWslRun = false;
        
        if (!TryFindLinker(out var linker, out var displayName, out var subCommand))
        {
            error = BuildInstallErrorMessage();
            return false;
        }

        try
        {
            string args;
            bool isWsl = linker == "wsl";
            bool isMingw = !isWsl && (linker.Contains("mingw") || linker.Contains("w64"));

            string linkFlag = shared ? "-shared" : "-no-pie";

            string linkExtras = FormatExtraLibraries(extraLibraries, isWindows: false);

            if (isWsl)
            {
                requiresWslRun = !shared;
                string wslObjectFile = ToWslPath(objectFile);
                string wslOutput = ToWslPath(output);

                args = shared
                    ? $"gcc -nostdlib {linkFlag} -o \"{wslOutput}\" \"{wslObjectFile}\" {linkExtras}"
                    : $"gcc -nostdlib {linkFlag} -o \"{wslOutput}\" \"{wslObjectFile}\" {linkExtras}";
            }
            else if (isMingw)
            {
                args = $"-nostdlib {linkFlag} -o \"{output}\" \"{objectFile}\" {linkExtras}";
            }
            else
            {
                if (linker.Contains("gcc"))
                    args = $"-nostdlib {linkFlag} -o \"{output}\" \"{objectFile}\" {linkExtras}";
                else
                    args = $"-o \"{output}\" \"{objectFile}\" {linkExtras}";
            }

            var psi = new ProcessStartInfo
            {
                FileName = linker,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                error = "Failed to start linker process.";
                return false;
            }

            process.WaitForExit();
            var stderr = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                error = $"Linking failed (using {displayName}):\n{stderr}\n\n{BuildInstallErrorMessage()}";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Linker error: {ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// Converts a Windows absolute path to a WSL path.
    /// e.g., C:\path\to\file -> /mnt/c/path/to/file
    /// </summary>
    /// <summary>
    /// Tries to find a working linker that can produce Windows x64 PE/COFF executables.
    /// </summary>
    public static bool TryFindWindowsLinker(out string path, out string displayName, out string versionArgs)
    {
        foreach (var candidate in WindowsLinkerCandidates)
        {
            string args = string.IsNullOrEmpty(candidate.SubCommand)
                ? candidate.VersionArg
                : $"{candidate.SubCommand} {candidate.VersionArg}";

            if (TryRunVersionProbe(candidate.Command, args, out var resolvedPath))
            {
                path = resolvedPath;
                displayName = candidate.DisplayName;
                versionArgs = candidate.SubCommand;
                return true;
            }
        }

        path = string.Empty;
        displayName = string.Empty;
        versionArgs = string.Empty;
        return false;
    }

    /// <summary>
    /// Links a COFF object file into a Windows PE executable or DLL.
    /// Uses lld-link or MSVC link.exe.
    /// </summary>
    public static bool TryLinkWindows(string objectFile, string output, out string error, bool shared = false,
        IEnumerable<string>? extraLibraries = null, bool emitDebugInfo = false)
    {
        if (!TryFindWindowsLinker(out var linker, out var displayName, out var subCommand))
        {
            error = BuildWindowsInstallErrorMessage();
            return false;
        }

        try
        {
            bool isLld = linker.ToLowerInvariant().Contains("lld-link");
            bool isMsvc = linker.ToLowerInvariant().Contains("link");

            string entryFlag = "/ENTRY:_start";
            string subsystemFlag = "/SUBSYSTEM:CONSOLE";
            string fixedImageFlags = "/DYNAMICBASE:NO /FIXED";
            string kernel32Lib = "kernel32.lib";

            string debugFlag = emitDebugInfo ? "/DEBUG " : "";
            string linkExtras = FormatExtraLibraries(extraLibraries, isWindows: true);

            string args;
            if (shared)
            {
                args = isLld
                    ? $"/NOLOGO {debugFlag}/DLL {entryFlag} {subsystemFlag} {fixedImageFlags} /OUT:\"{output}\" \"{objectFile}\" {kernel32Lib} {linkExtras}"
                    : $"/NOLOGO {debugFlag}/DLL {entryFlag} {subsystemFlag} {fixedImageFlags} /OUT:\"{output}\" \"{objectFile}\" {kernel32Lib} {linkExtras}";
            }
            else
            {
                args = isLld
                    ? $"/NOLOGO {debugFlag}{entryFlag} {subsystemFlag} {fixedImageFlags} /OUT:\"{output}\" \"{objectFile}\" {kernel32Lib} {linkExtras}"
                    : $"/NOLOGO {debugFlag}{entryFlag} {subsystemFlag} {fixedImageFlags} /OUT:\"{output}\" \"{objectFile}\" {kernel32Lib} {linkExtras}";
            }

            var psi = new ProcessStartInfo
            {
                FileName = linker,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                error = "Failed to start linker process.";
                return false;
            }

            process.WaitForExit();
            var stderr = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                error = $"Linking failed (using {displayName}):\n{stderr.Trim()}";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = $"Linker error: {ex.Message}";
            return false;
        }
    }

    private static string BuildWindowsInstallErrorMessage()
    {
        return @"No Windows linker found (lld-link or link.exe).

Options to fix:
1. LLVM/LLD (Recommended):
   winget install -e --id LLVM.LLVM
   # Adds lld-link to PATH

2. Visual Studio Build Tools:
   https://visualstudio.microsoft.com/downloads/
   # Install 'Desktop development with C++' workload

3. Chocolatey:
   choco install llvm
";
    }

    private static string FormatExtraLibraries(IEnumerable<string>? extraLibraries, bool isWindows)
    {
        if (extraLibraries == null)
            return string.Empty;

        var parts = new List<string>();
        foreach (var lib in extraLibraries)
        {
            if (string.IsNullOrWhiteSpace(lib))
                continue;
            if (isWindows &&
                !lib.EndsWith(".lib", StringComparison.OrdinalIgnoreCase) &&
                !lib.EndsWith(".obj", StringComparison.OrdinalIgnoreCase) &&
                !lib.Contains('\\') && !lib.Contains('/'))
            {
                parts.Add(lib + ".lib");
            }
            else
            {
                parts.Add(lib.Contains(' ') ? $"\"{lib}\"" : lib);
            }
        }

        return parts.Count == 0 ? string.Empty : string.Join(' ', parts);
    }

    public static string ToWslPath(string windowsPath)
    {
        // If already a Unix-style path, return as-is
        if (windowsPath.StartsWith("/"))
            return windowsPath;

        // Must be absolute Windows path like C:\...
        if (windowsPath.Length >= 3 && windowsPath[1] == ':' && windowsPath[2] == '\\')
        {
            char drive = char.ToLowerInvariant(windowsPath[0]);
            string path = windowsPath.Substring(3).Replace('\\', '/');
            return $"/mnt/{drive}/{path}";
        }

        // If relative or unknown format, return as-is (let the linker handle it)
        return windowsPath;
    }

    private static string BuildInstallErrorMessage()
    {
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        
        if (isWindows)
        {
            return @"No linker found for Linux x64 ELF targets.

Options to fix:
1. WSL2 (Recommended):
   wsl --install -d Ubuntu
   # Then inside WSL: sudo apt update && sudo apt install -y gcc nasm

2. MinGW-w64 (Native Windows):
   winget install -e --id mingw-w64.mingw-w64
   # Or: choco install mingw-w64

3. Docker:
   docker run --rm -v ${PWD}:/src gcc:latest bash -c 'cd /src && gcc -nostdlib -o hello hello.o'
";
        }
        else
        {
            return "No linker found (gcc or ld). Please install build-essential or similar.";
        }
    }
}
