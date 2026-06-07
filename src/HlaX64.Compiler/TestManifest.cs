using System.Text.Json;

namespace HlaX64.Compiler;

/// <summary>
/// Represents a test manifest for the HLA-X64 test runner.
/// </summary>
public sealed class TestManifest
{
    public string Name { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? ExpectedStdout { get; set; }
    public int ExpectedExitCode { get; set; } = 0;
    public string? Description { get; set; }
    public int TimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Absolute path to the manifest JSON file. Set automatically by
    /// <see cref="LoadFromJson"/> and <see cref="LoadAll"/>. Used by the
    /// runner to resolve <see cref="Source"/> relative to the manifest.
    /// </summary>
    public string? ManifestPath { get; set; }

    /// <summary>
    /// Load a test manifest from a JSON file.
    /// </summary>
    public static TestManifest LoadFromJson(string path)
    {
        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<TestManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        }) ?? throw new InvalidOperationException($"Failed to deserialize test manifest from '{path}'");
        manifest.ManifestPath = Path.GetFullPath(path);
        return manifest;
    }

    /// <summary>
    /// Load all test manifests from a directory (recursively).
    /// </summary>
    public static List<TestManifest> LoadAll(string directory)
    {
        var manifests = new List<TestManifest>();
        directory = ResolveDirectory(directory);

        if (!Directory.Exists(directory))
            return manifests;

        foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                manifests.Add(LoadFromJson(file));
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to load test manifest '{file}': {ex.Message}");
            }
        }

        return manifests;
    }

    private static string ResolveDirectory(string directory)
    {
        if (Path.IsPathRooted(directory))
            return directory;

        // Anchor repo-relative paths to a stable location. The assembly base
        // directory is tried first because the process current directory can
        // be mutated concurrently (e.g. by parallel test execution), which
        // would otherwise make resolution non-deterministic. Both anchors walk
        // up to the repo root (marked by HlaX64.slnx).
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (!File.Exists(Path.Combine(dir.FullName, "HlaX64.slnx")))
                    continue;

                var candidate = Path.Combine(dir.FullName, directory);
                if (Directory.Exists(candidate))
                    return candidate;
            }
        }

        // Fall back to a CWD-relative path, snapshotted to absolute so a later
        // working-directory change cannot affect enumeration.
        if (Directory.Exists(directory))
            return Path.GetFullPath(directory);

        return directory;
    }

    /// <summary>
    /// Save this manifest to a JSON file.
    /// </summary>
    public void SaveToJson(string path)
    {
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        File.WriteAllText(path, json);
    }
}