using System.Runtime.InteropServices;
using System.Text;
using HlaX64.Cli.Toolchain;

namespace HlaX64.AssemblyLab.Services;

public sealed record LabToolchainInfo(
    bool WslAvailable,
    bool WindowsLinkerAvailable,
    string? WindowsLinkerPath,
    bool LinuxLinkerAvailable,
    string? LinuxLinkerPath,
    bool AutoUseWslForLinux);

public static class LabToolchainService
{
    public static LabToolchainInfo Detect()
    {
        var wsl = LinkerTool.IsWslAvailable();
        var hasWindows = LinkerTool.TryFindWindowsLinker(out var winPath, out _, out _);
        var hasLinux = LinkerTool.TryFindLinker(out var linuxPath, out _, out _);
        var autoWsl = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && wsl
            && linuxPath == "wsl";

        return new LabToolchainInfo(
            wsl,
            hasWindows,
            hasWindows ? winPath : null,
            hasLinux,
            hasLinux ? linuxPath : null,
            autoWsl);
    }

    public static string Summarize(LabToolchainInfo info)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Toolchain (auto-detected)");
        sb.AppendLine($"WSL: {(info.WslAvailable ? "available" : "not found")}");
        sb.AppendLine(info.WindowsLinkerAvailable
            ? $"Windows linker: {info.WindowsLinkerPath}"
            : "Windows linker: not found (install LLVM or VS Build Tools)");
        sb.AppendLine(info.LinuxLinkerAvailable
            ? $"Linux linker: {info.LinuxLinkerPath}"
            : "Linux linker: not found (WSL gcc or native gcc)");
        sb.AppendLine($"Run Linux target via WSL: {(info.AutoUseWslForLinux ? "auto (yes)" : "auto (no)")}");
        return sb.ToString().TrimEnd();
    }
}
