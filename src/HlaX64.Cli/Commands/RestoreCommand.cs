using System.ComponentModel;
using System.Text.Json;
using HlaX64.Cli.Json;
using HlaX64.Cli.Project;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class RestoreCommand : Command<RestoreCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[manifest]")]
        public string? ManifestPath { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }

        [CommandOption("--no-git")]
        [Description("Skip git dependencies")]
        public bool NoGit { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var path = settings.ManifestPath ?? FindManifest(Directory.GetCurrentDirectory());
        if (path == null || !File.Exists(path))
        {
            Report(settings, false, error: "No hla64.toml manifest found.");
            return 1;
        }

        try
        {
            var manifestDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
            var manifest = ProjectManifest.Load(path);
            var lockDoc = DependencyResolver.Resolve(manifest, manifestDir, allowGit: !settings.NoGit);
            var lockPath = Path.Combine(manifestDir, "hla64.lock");
            DependencyResolver.SaveLock(lockDoc, lockPath);

            Report(settings, true, new
            {
                manifest = manifest.Name,
                version = manifest.Version,
                target = manifest.Target,
                sources = manifest.Sources,
                dependencies = lockDoc.Dependencies.Select(d => new
                {
                    d.Name,
                    d.Version,
                    d.Rev,
                    d.ContentHash,
                    resolvedPath = d.ResolvedPath,
                    sourceCount = d.Sources.Count
                }),
                lockFile = lockPath,
                manifestHash = lockDoc.ManifestHash
            });
            return 0;
        }
        catch (Exception ex)
        {
            Report(settings, false, error: ex.Message);
            return 1;
        }
    }

    private static string? FindManifest(string dir)
    {
        var direct = Path.Combine(dir, "hla64.toml");
        return File.Exists(direct) ? direct : null;
    }

    private static void Report(Settings settings, bool success, object? result = null, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                result,
                error
            });
            return;
        }

        if (!success)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        Console.WriteLine("Restore complete — wrote hla64.lock (see --json).");
    }
}
