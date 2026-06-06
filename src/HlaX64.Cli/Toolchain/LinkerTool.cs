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

    private sealed record LinkerInfo(
        string Command,
        string SubCommand,
        string VersionArg,
        string DisplayName,
        string InstallHint);

    /// <summary>
    /// Tries to find a working linker that can produce Linux x64 ELF executables.
    /// </summary>
    public static bool TryFindLinker(out string path, out string displayName, out string versionArgs)
    {
        foreach (var candidate in LinkerCandidates)
        {
            try
            {
                string fileName = candidate.Command;
                string args = string.IsNullOrEmpty(candidate.SubCommand) 
                    ? candidate.VersionArg 
                    : $"{candidate.SubCommand} {candidate.VersionArg}";

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(3000);
                    if (process.ExitCode == 0)
                    {
                        path = fileName;
                        displayName = candidate.DisplayName;
                        versionArgs = candidate.SubCommand; // empty for direct gcc, "gcc" for wsl
                        return true;
                    }
                }
            }
            catch
            {
                // Try next candidate
            }
        }

        path = string.Empty;
        displayName = string.Empty;
        versionArgs = string.Empty;
        return false;
    }

    /// <summary>
    /// Links an object file into an executable using the detected linker.
    /// Returns true if linking succeeded. Sets requiresWslRun if the executable must be run via WSL.
    /// </summary>
    public static bool TryLink(string objectFile, string executable, out string error, out bool requiresWslRun)
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

            if (isWsl)
            {
                requiresWslRun = true;
                // Convert Windows paths to WSL paths
                string wslObjectFile = ToWslPath(objectFile);
                string wslExecutable = ToWslPath(executable);
                
                // WSL: wsl gcc -nostdlib -no-pie -o executable objectFile
                // -nostdlib: don't link with C runtime (we provide our own _start)
                // -no-pie: avoid PIE issues with raw object files
                args = $"gcc -nostdlib -no-pie -o \"{wslExecutable}\" \"{wslObjectFile}\"";
            }
            else if (isMingw)
            {
                // MinGW targeting ELF: gcc -nostdlib -no-pie -o executable objectFile (with -f elf64 from NASM)
                args = $"-nostdlib -no-pie -o \"{executable}\" \"{objectFile}\"";
            }
            else
            {
                // Native Linux
                if (linker.Contains("gcc"))
                {
                    args = $"-nostdlib -no-pie -o \"{executable}\" \"{objectFile}\"";
                }
                else
                {
                    args = $"-o \"{executable}\" \"{objectFile}\"";
                }
            }

            var psi = new ProcessStartInfo
            {
                FileName = linker,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
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
