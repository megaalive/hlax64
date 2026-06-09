using HlaX64.AssemblyLab.Services;

namespace HlaX64.AssemblyLab.Tests;

public class CliExecutableResolverTests
{
    [Fact]
    public void FormatHla64Command_Quotes_Paths_With_Spaces()
    {
        var cmd = CliExecutableResolver.FormatHla64Command(
            "build",
            @"C:\work\my file.hla64",
            "windows-x64-msabi");

        Assert.Equal(@"hla64 build ""C:\work\my file.hla64"" --target windows-x64-msabi", cmd);
    }

    [Fact]
    public void TryCreateProcessStartInfo_Rejects_Empty_Command()
    {
        var ok = CliExecutableResolver.TryCreateProcessStartInfo(
            "   ",
            null,
            null,
            out _,
            out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryCreateProcessStartInfo_Wraps_Generic_Commands_In_Shell()
    {
        var ok = CliExecutableResolver.TryCreateProcessStartInfo(
            "echo hello",
            null,
            Directory.GetCurrentDirectory(),
            out var psi,
            out var error);

        Assert.True(ok, error);
        if (OperatingSystem.IsWindows())
        {
            Assert.Equal("cmd.exe", psi.FileName);
            Assert.Contains("echo hello", psi.Arguments);
        }
        else
        {
            Assert.Equal("/bin/sh", psi.FileName);
            Assert.Contains("echo hello", psi.Arguments);
        }
    }
}

public class PlatformShellProfileTests
{
    [Fact]
    public void Resolve_Returns_Platform_Shell()
    {
        var shell = PlatformShellProfile.Resolve();
        Assert.False(string.IsNullOrWhiteSpace(shell.Executable));
        Assert.False(string.IsNullOrWhiteSpace(shell.DisplayName));
    }

    [Fact]
    public void BuildEnvironment_Includes_Term_And_Lab_Flags()
    {
        var env = PlatformShellProfile.BuildEnvironment(Environment.CurrentDirectory, Directory.GetCurrentDirectory());
        Assert.Equal("xterm-256color", env["TERM"]);
        Assert.Equal("1", env["HLAX64_LAB"]);
    }
}
