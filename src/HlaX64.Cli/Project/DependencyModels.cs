namespace HlaX64.Cli.Project;

public sealed class DependencySpec
{
    public string Name { get; set; } = "";
    public string? Path { get; set; }
    public string? Git { get; set; }
    public string? Rev { get; set; }
    public string? Version { get; set; }
}

public sealed class ResolvedDependency
{
    public string Name { get; set; } = "";
    public string? Version { get; set; }
    public string? Rev { get; set; }
    public string ContentHash { get; set; } = "";
    public string ResolvedPath { get; set; } = "";
    public List<string> Sources { get; set; } = [];
}

public sealed class LockFileDocument
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string ManifestHash { get; set; } = "";
    public string ResolvedAt { get; set; } = "";
    public List<ResolvedDependency> Dependencies { get; set; } = [];
    public List<string> SourceHashes { get; set; } = [];
}
