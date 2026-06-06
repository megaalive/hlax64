using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class VerifyReproducibleCommand : Command<VerifyReproducibleCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<build-metadata>")]
        public string BuildMetadataPath { get; set; } = string.Empty;

        [CommandOption("--source")]
        public string? Source { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.BuildMetadataPath))
        {
            Report(settings, false, error: "build.json not found.");
            return 1;
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(settings.BuildMetadataPath));
        var root = doc.RootElement;
        var expectedHash = root.TryGetProperty("sourceSha256", out var h) ? h.GetString() : null;
        var expectedVersion = root.TryGetProperty("compilerVersion", out var v) ? v.GetString() : null;

        var sourcePath = settings.Source;
        if (sourcePath == null && root.TryGetProperty("source", out var srcEl))
            sourcePath = srcEl.GetString();
        string? actualHash = null;
        if (sourcePath != null && File.Exists(sourcePath))
            actualHash = Sha256Hex(File.ReadAllText(sourcePath));

        var versionOk = expectedVersion == Compilation.GetVersion();
        var hashOk = expectedHash == null || actualHash == expectedHash;
        var success = versionOk && hashOk;

        Report(settings, success, new
        {
            compilerVersionMatch = versionOk,
            sourceHashMatch = hashOk,
            expectedCompilerVersion = expectedVersion,
            actualCompilerVersion = Compilation.GetVersion(),
            expectedSourceHash = expectedHash,
            actualSourceHash = actualHash
        });
        return success ? 0 : 1;
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

        if (!success && error != null)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        Console.WriteLine(success ? "Reproducible build metadata verified." : "Reproducible build verification failed.");
    }
}
