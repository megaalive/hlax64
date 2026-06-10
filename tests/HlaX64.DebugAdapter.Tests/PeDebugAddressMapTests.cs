using System.Text.Json;
using HlaX64.Compiler.Debug;
using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class PeDebugAddressMapTests
{
    [Fact]
    public void Dnslookup_entry_maps_to_begin_line_not_mov()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        var mapPath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.hlamap.json");
        if (!File.Exists(exe) || !File.Exists(nasm) || !File.Exists(mapPath))
            return;

        var sourceMap = JsonSerializer.Deserialize<SourceMapDocument>(File.ReadAllText(mapPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var byAddress = PeDebugAddressMap.BuildSourceLinesByAddress(exe, nasm, sourceMap);
        Assert.True(byAddress.TryGetValue(0x140001000UL, out var entryLine), "missing entry address");
        Assert.Equal(17, entryLine);

        var lookup = PeDebugAddressMap.LookupSourceLine(0x140001000UL, byAddress);
        Assert.Equal(17, lookup);

        var step2 = PeDebugAddressMap.LookupSourceLine(0x140001002UL, byAddress);
        Assert.Equal(18, step2);

        var step3 = PeDebugAddressMap.LookupSourceLine(0x140001006UL, byAddress);
        Assert.Equal(19, step3);

        var step4 = PeDebugAddressMap.LookupSourceLine(0x14000100aUL, byAddress);
        Assert.Equal(18, step4);

        var stepIf = PeDebugAddressMap.LookupSourceLine(0x140001020UL, byAddress);
        Assert.Equal(21, stepIf);

        Assert.False(PeDebugAddressMap.IsUserCodeAddress(0x1400012c0UL, exe, nasm));
        var runtimeStep = PeDebugAddressMap.LookupSourceLine(0x1400012c0UL, byAddress);
        Assert.NotNull(runtimeStep);
        var sourceLines = File.ReadAllLines(sourceMap!.Source);
        Assert.InRange(runtimeStep!.Value, 17, sourceLines.Length);
        Assert.NotEqual(38, runtimeStep.Value);

        Assert.True(byAddress.TryGetValue(0x140001028UL, out var thenLine));
        Assert.Equal(22, thenLine);
        Assert.All(byAddress.Values, line => Assert.NotEqual(38, line));
    }

    [Fact]
    public void IsAddressInMainModule_rejects_system_dll_rip()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        Assert.False(PeDebugAddressMap.IsAddressInMainModule(0x7ff97927cef0UL, exe, nasm));
    }

    [Fact]
    public void GetOrBuild_reuses_cached_maps()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        PeDebugAddressMap.InvalidateCache();
        var first = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        var second = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        Assert.Same(first, second);
    }

    [Fact]
    public void IsPlausibleCallReturnAddress_matches_return_after_call()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        var maps = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        Assert.True(PeDebugAddressMap.IsPlausibleCallReturnAddress(0x14000100fUL, maps.NasmByAddress));
        Assert.False(PeDebugAddressMap.IsPlausibleCallReturnAddress(0UL, maps.NasmByAddress));
    }

    [Fact]
    public void TryFindStackReturnAddress_skips_saved_registers()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var exe = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.exe");
        var nasm = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "build", "dnslookup", "dnslookup.nasm");
        if (!File.Exists(exe) || !File.Exists(nasm))
            return;

        var maps = PeDebugAddressMap.GetOrBuild(exe, nasm, null);
        const ulong rsp = 0x1000;
        var slots = new Dictionary<ulong, ulong>
        {
            [rsp] = 0,
            [rsp + 8] = 0x7ff000000000,
            [rsp + 16] = 0x14000100f,
        };

        bool ReadSlot(ulong address, out ulong value)
        {
            if (slots.TryGetValue(address, out value))
                return true;

            value = 0;
            return false;
        }

        Assert.True(PeDebugAddressMap.TryFindStackReturnAddress(
            rsp,
            ReadSlot,
            exe,
            nasm,
            maps,
            out var returnAddress));
        Assert.Equal(0x14000100fUL, returnAddress);
    }
}
