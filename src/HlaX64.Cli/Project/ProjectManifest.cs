namespace HlaX64.Cli.Project;

public sealed class ProjectManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string Target { get; set; } = "linux-x64-sysv";
    public Dictionary<string, string> Sources { get; set; } = new();
    public List<string> Dependencies { get; set; } = [];
    public List<DependencySpec> DependencySpecs { get; set; } = [];

    public static ProjectManifest Load(string path)
    {
        var manifest = new ProjectManifest();
        var lines = File.ReadAllLines(path);
        bool inDeps = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;

            if (trimmed.Equals("[dependencies]", StringComparison.OrdinalIgnoreCase))
            {
                inDeps = true;
                continue;
            }

            if (trimmed.StartsWith('['))
            {
                inDeps = false;
                continue;
            }

            if (inDeps)
            {
                ParseDependencyLine(trimmed, manifest);
                continue;
            }

            var eq = trimmed.IndexOf('=');
            if (eq <= 0) continue;
            var key = trimmed[..eq].Trim();
            var value = trimmed[(eq + 1)..].Trim().Trim('"');

            switch (key)
            {
                case "name": manifest.Name = value; break;
                case "version": manifest.Version = value; break;
                case "target": manifest.Target = value; break;
                default: manifest.Sources[key] = value; break;
            }
        }

        if (manifest.Sources.Count == 0)
            manifest.Sources["main"] = "main.hla64";

        return manifest;
    }

    private static void ParseDependencyLine(string line, ProjectManifest manifest)
    {
        var eq = line.IndexOf('=');
        if (eq <= 0) return;
        var name = line[..eq].Trim();
        var value = line[(eq + 1)..].Trim();

        var spec = new DependencySpec { Name = name };
        if (value.StartsWith('{') && value.EndsWith('}'))
        {
            foreach (var part in value.Trim('{', '}').Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;
                var k = kv[0].Trim();
                var v = kv[1].Trim().Trim('"');
                switch (k)
                {
                    case "path": spec.Path = v; break;
                    case "git": spec.Git = v; break;
                    case "rev": spec.Rev = v; break;
                    case "version": spec.Version = v; break;
                }
            }
        }
        else
        {
            spec.Path = value.Trim('"');
        }

        manifest.DependencySpecs.Add(spec);
        manifest.Dependencies.Add(name);
    }
}
