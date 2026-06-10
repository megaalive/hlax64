using System.Text.Json;
using HlaX64.Compiler.Debug;
using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class ProgramShutdownPhaseTests
{
    [Fact]
    public void IsProgramShutdownPhase_true_for_system_dll_after_user_code()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        var maps = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        Assert.True(PeDebugAddressMap.IsProgramShutdownPhase(
            0x7ff97927cef0UL, exe, nasm, maps, callSiteLineFromStack: null));
    }

    [Fact]
    public void IsProgramShutdownPhase_false_for_hlax_runtime_with_call_site()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        var maps = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        Assert.False(PeDebugAddressMap.IsProgramShutdownPhase(
            0x1400012c0UL, exe, nasm, maps, callSiteLineFromStack: 18));
    }

    [Fact]
    public void IsExitJumpTarget_detects_exit_stub_jump()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        var maps = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        Assert.True(PeDebugAddressMap.IsExitJumpTarget(0x140001a70UL, exe, nasm, maps));
        Assert.False(PeDebugAddressMap.IsExitJumpTarget(0x1400012c0UL, exe, nasm, maps));
    }
}
