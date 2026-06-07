using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HlaX64.Cli.Project;

public sealed class DependencyResolver
{
    public static string Sha256Hex(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static LockFileDocument Resolve(ProjectManifest manifest, string manifestDir, bool allowGit = true)
    {
        var lockDoc = new LockFileDocument
        {
            Name = manifest.Name,
            Version = manifest.Version,
            ManifestHash = Sha256Hex(File.ReadAllText(Path.Combine(manifestDir, "hla64.toml"))),
            ResolvedAt = DateTime.UtcNow.ToString("o")
        };

        foreach (var rel in manifest.Sources.Values)
        {
            var full = Path.Combine(manifestDir, rel);
            if (File.Exists(full))
                lockDoc.SourceHashes.Add($"{rel}:{Sha256File(full)}");
        }

        foreach (var dep in manifest.DependencySpecs)
        {
            var resolved = ResolveOne(dep, manifestDir, allowGit);
            lockDoc.Dependencies.Add(resolved);
        }

        return lockDoc;
    }

    private static ResolvedDependency ResolveOne(DependencySpec spec, string manifestDir, bool allowGit)
    {
        string resolvedPath;
        if (!string.IsNullOrWhiteSpace(spec.Path))
        {
            resolvedPath = Path.GetFullPath(Path.Combine(manifestDir, spec.Path));
            if (!Directory.Exists(resolvedPath))
                throw new InvalidOperationException($"Path dependency '{spec.Name}' not found: {resolvedPath}");
        }
        else if (!string.IsNullOrWhiteSpace(spec.Git))
        {
            if (!allowGit)
                throw new InvalidOperationException($"Git dependency '{spec.Name}' requires git on PATH (deferred on this platform).");
            if (!IsGitAvailable())
                throw new InvalidOperationException($"Git dependency '{spec.Name}' requires git on PATH.");
            var depsRoot = Path.Combine(manifestDir, ".hla64", "deps");
            Directory.CreateDirectory(depsRoot);
            resolvedPath = Path.Combine(depsRoot, spec.Name);
            CloneOrUpdateGit(spec.Git, spec.Rev, resolvedPath);
        }
        else
        {
            throw new InvalidOperationException($"Dependency '{spec.Name}' must specify path or git.");
        }

        var depManifestPath = Path.Combine(resolvedPath, "hla64.toml");
        var sources = new List<string>();

        if (File.Exists(depManifestPath))
        {
            var depManifest = ProjectManifest.Load(depManifestPath);
            foreach (var rel in depManifest.Sources.Values)
            {
                var src = Path.Combine(resolvedPath, rel);
                if (File.Exists(src))
                    sources.Add(src);
            }
        }
        else
        {
            sources.AddRange(Directory.GetFiles(resolvedPath, "*.hla64", SearchOption.AllDirectories));
        }

        return new ResolvedDependency
        {
            Name = spec.Name,
            Version = spec.Version,
            Rev = spec.Rev,
            ContentHash = HashDependencyAtPath(resolvedPath),
            ResolvedPath = resolvedPath,
            Sources = sources
        };
    }

    private static bool IsGitAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(3000);
            return p?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void CloneOrUpdateGit(string url, string? rev, string targetDir)
    {
        if (Directory.Exists(Path.Combine(targetDir, ".git")))
        {
            RunGit(targetDir, "fetch --depth 1 origin");
            if (!string.IsNullOrWhiteSpace(rev))
                RunGit(targetDir, $"checkout {rev}");
            else
                RunGit(targetDir, "pull --depth 1");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(targetDir)!);
        var cloneArgs = string.IsNullOrWhiteSpace(rev)
            ? $"clone --depth 1 \"{url}\" \"{targetDir}\""
            : $"clone --depth 1 \"{url}\" \"{targetDir}\"";
        RunGit(manifestDir: null, cloneArgs);
        if (!string.IsNullOrWhiteSpace(rev))
            RunGit(targetDir, $"checkout {rev}");
    }

    private static void RunGit(string? manifestDir, string args)
    {
        var psi = new ProcessStartInfo("git", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        if (manifestDir != null)
            psi.WorkingDirectory = manifestDir;
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start git.");
        p.WaitForExit(120_000);
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"git {args} failed: {p.StandardError.ReadToEnd()}");
    }

    public static LockFileDocument LoadLock(string lockPath)
    {
        var json = File.ReadAllText(lockPath);
        return JsonSerializer.Deserialize<LockFileDocument>(json, JsonOptions())
            ?? throw new InvalidOperationException("Invalid hla64.lock");
    }

    public static void SaveLock(LockFileDocument doc, string lockPath)
    {
        File.WriteAllText(lockPath, JsonSerializer.Serialize(doc, JsonOptions()));
    }

    public static bool VerifyLock(ProjectManifest manifest, string manifestDir, LockFileDocument lockDoc)
    {
        var manifestPath = Path.Combine(manifestDir, "hla64.toml");
        if (!File.Exists(manifestPath)) return false;
        var hash = Sha256Hex(File.ReadAllText(manifestPath));
        if (!string.Equals(hash, lockDoc.ManifestHash, StringComparison.OrdinalIgnoreCase))
            return false;

        foreach (var dep in lockDoc.Dependencies)
        {
            if (!Directory.Exists(dep.ResolvedPath))
                return false;
            var currentHash = HashDependencyAtPath(dep.ResolvedPath);
            if (!string.Equals(currentHash, dep.ContentHash, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        return true;
    }

    public static string HashDependencyAtPath(string resolvedPath)
    {
        var hashBuilder = new StringBuilder();
        var depManifestPath = Path.Combine(resolvedPath, "hla64.toml");
        if (File.Exists(depManifestPath))
        {
            hashBuilder.Append(Sha256Hex(File.ReadAllText(depManifestPath)));
            var depManifest = ProjectManifest.Load(depManifestPath);
            foreach (var rel in depManifest.Sources.Values)
            {
                var src = Path.Combine(resolvedPath, rel);
                if (File.Exists(src))
                    hashBuilder.Append(':').Append(Sha256File(src));
            }
        }
        else
        {
            foreach (var file in Directory.GetFiles(resolvedPath, "*.hla64", SearchOption.AllDirectories).OrderBy(f => f))
                hashBuilder.Append(':').Append(Sha256File(file));
        }

        return Sha256Hex(hashBuilder.ToString());
    }

    public static IEnumerable<string> TopologicalSources(LockFileDocument lockDoc)
    {
        foreach (var dep in lockDoc.Dependencies)
            foreach (var src in dep.Sources.OrderBy(s => s, StringComparer.OrdinalIgnoreCase))
                yield return src;
    }

    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true };
}
