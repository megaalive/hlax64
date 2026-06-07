using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
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
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var path = settings.ManifestPath ?? FindManifest(Directory.GetCurrentDirectory());
        if (path == null || !File.Exists(path))
        {
            Report(settings, false, error: "No hla64.toml manifest found.");
            return 1;
        }

        var manifestText = File.ReadAllText(path);
        var manifest = ProjectManifest.Load(path);
        var manifestDir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        var lockPath = Path.Combine(manifestDir, "hla64.lock");
        var hash = Sha256Hex(manifestText);

        var lockDoc = new
        {
            schemaVersion = 1,
            name = manifest.Name,
            version = manifest.Version,
            manifestHash = hash,
            manifestPath = Path.GetFileName(path),
            resolvedAt = DateTime.UtcNow.ToString("o"),
            sources = manifest.Sources,
            dependencies = manifest.Dependencies,
            note = "Lock file records resolved manifest hash; dependency resolution deferred post-MVP"
        };
        File.WriteAllText(lockPath, JsonSerializer.Serialize(lockDoc, new JsonSerializerOptions { WriteIndented = true }));

        Report(settings, true, new
        {
            manifest = manifest.Name,
            version = manifest.Version,
            target = manifest.Target,
            sources = manifest.Sources,
            dependencies = manifest.Dependencies,
            lockFile = lockPath,
            manifestHash = hash
        });
        return 0;
    }

    private static string? FindManifest(string dir)
    {
        var direct = Path.Combine(dir, "hla64.toml");
        return File.Exists(direct) ? direct : null;
    }

    private static string Sha256Hex(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
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
