using System.Diagnostics;

namespace HlaX64.TestSupport;

public static class WslHostResolver
{
    public static string? TryGetHostIpForWsl()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "wsl",
                Arguments = "bash -lc \"grep -m1 nameserver /etc/resolv.conf | awk '{print $2}'\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = Process.Start(startInfo);
            if (process == null)
                return null;
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(5000);
            return process.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }
}
