using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class X64CallInstructionTests
{
    [Theory]
    [InlineData(new byte[] { 0xE8, 0xB1, 0x02, 0x00, 0x00 }, 5)]
    [InlineData(new byte[] { 0xFF, 0x15, 0x10, 0x20, 0x00, 0x00 }, 6)]
    public void TryGetCallInstructionLength_decodes_common_calls(byte[] bytes, int expected)
    {
        Assert.Equal(expected, X64CallInstruction.TryGetCallInstructionLength(bytes));
    }

    [Fact]
    public void LookupCallSiteSourceLine_maps_return_address_to_call_source()
    {
        var map = new Dictionary<ulong, int>
        {
            [0x14000100aUL] = 18,
            [0x14000100fUL] = 18
        };

        var line = PeDebugAddressMap.LookupCallSiteSourceLine(0x14000100fUL, map);
        Assert.Equal(18, line);
    }
}
