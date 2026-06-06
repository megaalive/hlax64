using System.Diagnostics;

namespace HlaX64.Cli.Toolchain;

/// <summary>
/// Detects and invokes the system linker (gcc or ld) for Linux x64.
/// </summary>
public sealed class LinkerTool
{
    public static bool TryFindLinker(out string path)
    {
        var candidates = new[] { "gcc", "ld", "/usr/bin/gcc", "/usr/bin/ld" };

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
            }
        }

        path = string.Empty;
        return false;
    }

    public static bool TryLink(string objectFile, string executable, out string error)
    {
        if (!TryFindLinker(out var linker))
        {
            // Try ld as fallback
            if (!TryFindLinker(out linker))
            {
                error = "No linker found (gcc or ld). Please install build-essential or similar.";
                return false;
            }
        }

        try
        {
            string args;
            if (linker.Contains("gcc"))
            {
                args = $"-nostdlib -o \"{executable}\" \"{objectFile}\"";
            }
            else
            {
                args = $"-o \"{executable}\" \"{objectFile}\"";
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
                error = $"Linking failed:\n{stderr}";
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
}