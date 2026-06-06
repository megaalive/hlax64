using System.ComponentModel;
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
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var path = settings.ManifestPath ?? FindManifest(Directory.GetCurrentDirectory());
        if (path == null || !File.Exists(path))
        {
            Report(settings, false, error: "No hla64.toml manifest found.");
            return 1;
        }

        var manifest = ProjectManifest.Load(path);
        Report(settings, true, new
        {
            manifest = manifest.Name,
            version = manifest.Version,
            target = manifest.Target,
            sources = manifest.Sources,
            dependencies = manifest.Dependencies,
            note = "Restore MVP stub — dependency resolution deferred to post-MVP"
        });
        return 0;
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

        Console.WriteLine("Restore complete (stub — see --json).");
    }
}
