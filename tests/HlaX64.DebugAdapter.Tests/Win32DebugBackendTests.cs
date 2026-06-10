using HlaX64.DebugAdapter;

namespace HlaX64.DebugAdapter.Tests;

public sealed class Win32DebugBackendTests
{
    [Fact]
    public void IsSupported_is_true_on_windows()
    {
        Assert.Equal(OperatingSystem.IsWindows(), Win32DebugBackend.IsSupported);
    }

    [Fact]
    public void Factory_prefers_win32_on_windows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var backend = DebugBackendFactory.CreateDefault();
        Assert.IsType<Win32DebugBackend>(backend);
        Assert.Equal("win32", backend.Name);
        Assert.True(backend.IsAvailable);
    }
}
