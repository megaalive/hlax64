using HlaX64.AssemblyLab.Services;
using HlaX64.TestSupport;

namespace HlaX64.AssemblyLab.Tests;

public sealed class ProgramLaunchArgumentsTests
{
    [Fact]
    public void ShouldPrompt_when_expected_arguments_file_exists()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "dnslookup.hla64");
        var projectFolder = Path.GetDirectoryName(sourcePath)!;
        var sourceText = File.ReadAllText(sourcePath);

        Assert.True(ProgramLaunchArguments.ShouldPrompt(sourceText, sourcePath, projectFolder));
    }

    [Fact]
    public void GetDefaultArgumentsText_resolves_localhost_for_dnslookup()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "dnslookup", "dnslookup.hla64");
        var projectFolder = Path.GetDirectoryName(sourcePath)!;

        var defaults = ProgramLaunchArguments.GetDefaultArgumentsText(sourcePath, projectFolder, repoRoot);
        Assert.Equal("localhost", defaults);
    }

    [Theory]
    [InlineData("localhost beta", new[] { "localhost", "beta" })]
    [InlineData("\"C:\\Program Files\\test.txt\"", new[] { @"C:\Program Files\test.txt" })]
    [InlineData("", new string[0])]
    public void Parse_splits_command_line_tokens(string input, string[] expected)
    {
        Assert.Equal(expected, ProgramLaunchArguments.Parse(input));
    }

    [Fact]
    public void ShouldPrompt_is_false_for_pid_without_argv()
    {
        var repoRoot = RealToolTestHarness.FindRepoRoot();
        var sourcePath = Path.Combine(repoRoot, "examples", "tools", "10-windows", "pid", "pid.hla64");
        if (!File.Exists(sourcePath))
            return;

        var sourceText = File.ReadAllText(sourcePath);
        var projectFolder = Path.GetDirectoryName(sourcePath)!;
        Assert.False(ProgramLaunchArguments.ShouldPrompt(sourceText, sourcePath, projectFolder));
    }
}
