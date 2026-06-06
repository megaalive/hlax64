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
        // Try common locations
        var candidates = new[]
        {
            "nasm",
            "/usr/bin/nasm",
            "/usr/local/bin/nasm",
            "/opt/homebrew/bin/nasm",
            // Local project NASM (from src/HlaX64.Cli/bin/Debug/net10.0/)
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "nasm", "nasm.exe"),
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "nasm", "nasm"),
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                using var process = Process.Start(psi);
                if (process != null)
                {
                    process.WaitForExit(2000);
                    if (process.ExitCode == 0)
                    {
                        path = candidate;
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

    public bool TryAssemble(string nasmSource, string objectFile, out string error)
    {
        if (!TryFindNasm(out var nasm))
        {
            error = "NASM not found. Please install NASM (https://nasm.us).";
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = nasm,
                Arguments = $"-f elf64 \"{nasmSource}\" -o \"{objectFile}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
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