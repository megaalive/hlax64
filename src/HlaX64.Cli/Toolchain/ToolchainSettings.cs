using System.Text.Json;

namespace HlaX64.Cli.Toolchain;

public sealed class ToolchainSettings
{
    public string? Hla64Path { get; set; }
    public string? RuntimeDirectory { get; set; }
    public string? NasmPath { get; set; }
    public string? WindowsLinkerPath { get; set; }
    public string? LinuxLinkerPath { get; set; }

    public static ToolchainSettings Empty { get; } = new();

    public static string GetDefaultSettingsPath()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "HlaX64", "AssemblyLab", "settings.json");
        }

        var configHome = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome))
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");

        return Path.Combine(configHome, "hlax64", "assemblylab", "settings.json");
    }

    public static ToolchainSettings Load(string? path = null)
    {
        path ??= GetDefaultSettingsPath();
        if (!File.Exists(path))
            return new ToolchainSettings();

        try
        {
            return JsonSerializer.Deserialize<ToolchainSettings>(
                       File.ReadAllText(path),
                       new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                   ?? new ToolchainSettings();
        }
        catch
        {
            return new ToolchainSettings();
        }
    }

    public void Save(string? path = null)
    {
        path ??= GetDefaultSettingsPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
