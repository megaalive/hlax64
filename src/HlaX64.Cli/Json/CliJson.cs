using System.Text.Json;
using System.Text.Json.Serialization;

namespace HlaX64.Cli.Json;

/// <summary>
/// Shared JSON output conventions for machine-readable CLI commands.
/// </summary>
public static class CliJson
{
    public const int SchemaVersion = 1;

    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static void Write(object payload) =>
        Console.WriteLine(JsonSerializer.Serialize(payload, Options));
}
