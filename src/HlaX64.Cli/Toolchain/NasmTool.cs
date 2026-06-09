using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace HlaX64.Cli.Toolchain;

/// <summary>
/// Detects and invokes NASM assembler.
/// </summary>
public sealed class NasmTool
{
    private readonly string? _nasmPath;

    public NasmTool(string? nasmPath = null)
    {
        _nasmPath = nasmPath;
    }

    public static bool TryFindNasm(out string path)
    {
        var resolved = ToolchainResolver.Default.ResolveNasm();
        if (resolved.Found && !string.IsNullOrWhiteSpace(resolved.Path))
        {
            path = resolved.Path;
            return true;
        }

        // Try common locations.
        // Each candidate is (executable, prefixArgs) where prefixArgs is the
        // sub-command to run nasm (e.g. "nasm" for WSL). The --version flag
        // is appended internally for detection and is NOT stored in the return
        // value, so callers never accidentally pass --version to actual
        // assembly commands.
        var candidates = new (string FileName, string PrefixArgs)[]
        {
            // Native nasm (uses Windows paths directly — preferred)
            ("nasm", ""),
            // Local project NASM (from repo root nasm/)
            (Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "nasm", "nasm.exe"), ""),
            (Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "nasm", "nasm"), ""),
            // WSL (works but needs WSL-path conversion — last resort)
            ("wsl", "nasm"),
            // Other Unix paths
            ("/usr/bin/nasm", ""),
            ("/usr/local/bin/nasm", ""),
            ("/opt/homebrew/bin/nasm", ""),
        };

        foreach (var (fileName, prefixArgs) in candidates)
        {
            try
            {
                // Version-detection args (not stored, only for probing)
                var versionArgs = string.IsNullOrEmpty(prefixArgs) ? "--version" : $"{prefixArgs} --version";

                var psi = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = versionArgs,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(3000);
                    if (process.ExitCode == 0)
                    {
                        // Prefix only — no --version, no --help.
                        // Callers can append real NASM flags.
                        path = string.IsNullOrEmpty(prefixArgs)
                            ? fileName
                            : $"{fileName}|{prefixArgs}";
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
        return false;
    }

    /// <summary>
    /// Parses a path returned by <see cref="TryFindNasm"/>, which may
    /// contain a pipe-separated argument prefix (e.g. "wsl|nasm --version").
    /// </summary>
    public static (string FileName, string Args) SplitNasmInvocation(string nasmPath)
    {
        var idx = nasmPath.IndexOf('|');
        if (idx < 0) return (nasmPath, string.Empty);
        return (nasmPath[..idx], nasmPath[(idx + 1)..]);
    }

    public bool TryAssemble(string nasmSource, string objectFile, out string error, string format = "elf64",
        bool emitDebugInfo = false)
    {
        var nasmPath = _nasmPath;
        if (string.IsNullOrEmpty(nasmPath) && !TryFindNasm(out nasmPath))
        {
            error = "NASM not found. Please install NASM (https://nasm.us).";
            return false;
        }

        try
        {
            var (fileName, prefixArgs) = SplitNasmInvocation(nasmPath!);
            bool isWsl = fileName == "wsl";

            // Convert Windows paths to WSL paths when running via WSL
            var srcArg = isWsl ? LinkerTool.ToWslPath(nasmSource) : nasmSource;
            var objArg = isWsl ? LinkerTool.ToWslPath(objectFile) : objectFile;

            var debugArgs = emitDebugInfo
                ? format.Equals("win64", StringComparison.OrdinalIgnoreCase)
                    ? "-g -F cv8 "
                    : "-g -F dwarf "
                : "";

            var nasmArgs = string.IsNullOrEmpty(prefixArgs)
                ? $"{debugArgs}-f {format} \"{srcArg}\" -o \"{objArg}\""
                : $"{prefixArgs} {debugArgs}-f {format} \"{srcArg}\" -o \"{objArg}\"";

            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = nasmArgs,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                error = "Failed to start NASM process.";
                return false;
            }

            process.WaitForExit();
            var stderr = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            {
                error = $"NASM assembly failed:\n{stderr}";
                return false;
            }

            error = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            error = $"NASM error: {ex.Message}";
            return false;
        }
    }
}