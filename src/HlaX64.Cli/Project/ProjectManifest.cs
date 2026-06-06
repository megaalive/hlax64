namespace HlaX64.Cli.Project;

public sealed class ProjectManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string Target { get; set; } = "linux-x64-sysv";
    public Dictionary<string, string> Sources { get; set; } = new();
    public List<string> Dependencies { get; set; } = [];

    public static ProjectManifest Load(string path)
    {
        var manifest = new ProjectManifest();
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#') || trimmed.Length == 0) continue;
            if (trimmed.StartsWith('[')) continue;

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
}
