using HlaX64.Cli.Toolchain;

namespace HlaX64.AssemblyLab.Tests;

public class ToolchainResolverTests
{
    [Fact]
    public void ResolveRuntimeDirectory_Finds_AppLocal_Runtime()
    {
        var previousRuntimeDir = Environment.GetEnvironmentVariable("HLAX64_RUNTIME_DIR");
        try
        {
            Environment.SetEnvironmentVariable("HLAX64_RUNTIME_DIR", null);

            var root = CreateTempRoot();
            var runtime = Path.Combine(root, "runtime", "linux-x64");
            Directory.CreateDirectory(runtime);
            File.WriteAllText(Path.Combine(runtime, "stdout.nasm"), "; test");

            var resolver = new ToolchainResolver(new ToolchainSettings(), root);
            var result = resolver.ResolveRuntimeDirectory();

            Assert.True(result.Found);
            Assert.Equal(ToolchainSource.Bundled, result.Source);
        }
        finally
        {
            Environment.SetEnvironmentVariable("HLAX64_RUNTIME_DIR", previousRuntimeDir);
        }
    }

    [Fact]
    public void ResolveNasm_Prefers_User_Setting()
    {
        var root = CreateTempRoot();
        var nasm = Path.Combine(root, OperatingSystem.IsWindows() ? "nasm.exe" : "nasm");
        File.WriteAllText(nasm, "");

        var resolver = new ToolchainResolver(new ToolchainSettings { NasmPath = nasm }, root);
        var result = resolver.ResolveNasm();

        Assert.True(result.Found);
        Assert.Equal(ToolchainSource.UserSetting, result.Source);
        Assert.Equal(Path.GetFullPath(nasm), result.Path);
    }

    [Fact]
    public void ToolchainSettings_RoundTrips_To_Json()
    {
        var root = CreateTempRoot();
        var path = Path.Combine(root, "settings.json");
        var settings = new ToolchainSettings
        {
            Hla64Path = "hla64",
            RuntimeDirectory = "runtime",
            NasmPath = "nasm",
            WindowsLinkerPath = "lld-link",
            LinuxLinkerPath = "gcc"
        };

        settings.Save(path);
        var loaded = ToolchainSettings.Load(path);

        Assert.Equal(settings.Hla64Path, loaded.Hla64Path);
        Assert.Equal(settings.RuntimeDirectory, loaded.RuntimeDirectory);
        Assert.Equal(settings.NasmPath, loaded.NasmPath);
        Assert.Equal(settings.WindowsLinkerPath, loaded.WindowsLinkerPath);
        Assert.Equal(settings.LinuxLinkerPath, loaded.LinuxLinkerPath);
    }

    private static string CreateTempRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "hlax64-toolchain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
