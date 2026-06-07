using HlaX64.Cli.Project;

namespace HlaX64.Cli.Commands;

public static class ProjectBuildHelper
{
    public static (string? SourceFile, string SourceText, string? ProjectDir, string? Error) ResolveProjectSource(
        string? explicitSource, bool requireLock = true)
    {
        if (!string.IsNullOrWhiteSpace(explicitSource))
        {
            var path = Path.GetFullPath(explicitSource);
            return (path, File.ReadAllText(path), Path.GetDirectoryName(path), null);
        }

        var cwd = Directory.GetCurrentDirectory();
        var manifestPath = Path.Combine(cwd, "hla64.toml");
        if (!File.Exists(manifestPath))
            return (null, "", null, "No source file or hla64.toml manifest found.");

        var manifest = ProjectManifest.Load(manifestPath);
        var lockPath = Path.Combine(cwd, "hla64.lock");
        LockFileDocument? lockDoc = null;
        if (File.Exists(lockPath))
            lockDoc = DependencyResolver.LoadLock(lockPath);
        else if (requireLock && manifest.DependencySpecs.Count > 0)
            return (null, "", cwd, "hla64.lock missing or stale — run 'hla64 restore' first.");

        if (lockDoc != null && !DependencyResolver.VerifyLock(manifest, cwd, lockDoc))
            return (null, "", cwd, "hla64.lock mismatch — run 'hla64 restore' to refresh.");

        var parts = new List<string>();
        string? primary = null;

        if (lockDoc != null)
        {
            foreach (var depSrc in DependencyResolver.TopologicalSources(lockDoc))
            {
                if (File.Exists(depSrc))
                {
                    parts.Add($"// --- dep:{Path.GetFileName(depSrc)} ---\n{File.ReadAllText(depSrc)}");
                }
            }
        }

        foreach (var rel in manifest.Sources.Values)
        {
            var full = Path.GetFullPath(Path.Combine(cwd, rel));
            if (File.Exists(full))
            {
                parts.Add($"// --- {rel} ---\n{File.ReadAllText(full)}");
                primary ??= full;
            }
        }

        if (primary == null)
            return (null, "", cwd, "No source files found in manifest.");

        return (primary, string.Join("\n\n", parts), cwd, null);
    }
}
