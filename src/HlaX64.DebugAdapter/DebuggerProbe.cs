using System.Diagnostics;

namespace HlaX64.DebugAdapter;

internal static class DebuggerProbe
{
    private static readonly string[] WindowsGdbCandidates =
    [
        @"C:\ProgramData\mingw64\mingw64\bin\gdb.exe",
        @"C:\msys64\mingw64\bin\gdb.exe",
        @"C:\msys64\usr\bin\gdb.exe",
        @"C:\MinGW\bin\gdb.exe",
    ];

    public static bool TryFindGdb(out string path)
    {
        path = "";

        if (OperatingSystem.IsLinux())
        {
            foreach (var candidate in new[] { "/usr/bin/gdb", "/usr/local/bin/gdb" })
            {
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }

            if (TryFindOnPath("gdb", out path))
                return true;
        }

        if (OperatingSystem.IsWindows())
        {
            foreach (var candidate in WindowsGdbCandidates)
            {
                if (File.Exists(candidate))
                {
                    path = candidate;
                    return true;
                }
            }

            if (TryFindOnPath("gdb.exe", out path))
                return true;
        }

        return false;
    }

    public static bool TryFindLldb(out string path)
    {
        path = "";
        if (!OperatingSystem.IsWindows())
            return false;

        var llvmPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "LLVM", "bin", "lldb.exe");
        if (File.Exists(llvmPath))
        {
            path = llvmPath;
            return true;
        }

        return TryFindOnPath("lldb.exe", out path);
    }

    /// <summary>LLVM's Windows LLDB is built against Python 3.11 and crashes without python311.dll.</summary>
    public static bool TryFindPython311ForLldb(string lldbPath, out string? pythonDllPath)
    {
        pythonDllPath = null;
        var lldbDir = Path.GetDirectoryName(lldbPath);
        if (lldbDir != null)
        {
            var adjacent = Path.Combine(lldbDir, "python311.dll");
            if (File.Exists(adjacent))
            {
                pythonDllPath = adjacent;
                return true;
            }
        }

        if (TryFindOnPath("python311.dll", out var onPath))
        {
            pythonDllPath = onPath;
            return true;
        }

        foreach (var root in new[]
                 {
                     Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python311"),
                     @"C:\Python311",
                 })
        {
            var candidate = Path.Combine(root, "python311.dll");
            if (File.Exists(candidate))
            {
                pythonDllPath = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool IsLldbUsable(out string? reason)
    {
        reason = null;
        if (!TryFindLldb(out var lldbPath))
        {
            reason = "lldb not found (install LLVM or add lldb.exe to PATH).";
            return false;
        }

        if (!TryFindPython311ForLldb(lldbPath, out _))
        {
            reason =
                "LLDB on Windows requires Python 3.11 (python311.dll). " +
                "Install Python 3.11 x64 from python.org, or use MinGW GDB instead.";
            return false;
        }

        if (!ProbeProcess(lldbPath, "--version"))
        {
            reason = "lldb failed to start (check Python 3.11 install).";
            return false;
        }

        return true;
    }

    public static string? GetUnavailableReason()
    {
        if (OperatingSystem.IsLinux())
        {
            return TryFindGdb(out _)
                ? null
                : "Install gdb (e.g. sudo apt install gdb).";
        }

        if (OperatingSystem.IsWindows())
        {
            if (TryFindGdb(out _))
                return null;

            if (TryFindLldb(out var lldbPath) && !TryFindPython311ForLldb(lldbPath, out _))
                return "LLDB needs Python 3.11 (python311.dll). Install Python 3.11 x64, or install MinGW GDB.";

            return "Install MinGW GDB (recommended) or LLVM LLDB with Python 3.11.";
        }

        return "No supported debugger for this platform.";
    }

    private static bool TryFindOnPath(string fileName, out string path)
    {
        path = "";
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(candidate))
            {
                path = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool ProbeProcess(string fileName, string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });
            if (process == null)
                return false;

            if (!process.WaitForExit(5000))
            {
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
