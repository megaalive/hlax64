using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class Win32ProcessCommandLineTests
{
    [Fact]
    public void BuildCreateProcessCommandLine_quotes_arguments_with_spaces()
    {
        var cmd = Win32ProcessCommandLine.BuildCreateProcessCommandLine(
            @"C:\build\dnslookup.exe",
            ["localhost", @"C:\Program Files\data.txt"]);

        Assert.Equal("\"C:\\build\\dnslookup.exe\" localhost \"C:\\Program Files\\data.txt\"", cmd);
    }
}
