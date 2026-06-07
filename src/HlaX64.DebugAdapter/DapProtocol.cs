using System.Text.Json;

namespace HlaX64.DebugAdapter;

public static class DapJson
{
    public static JsonElement? Parse(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        using var doc = JsonDocument.Parse(line);
        return doc.RootElement.Clone();
    }

    public static string Serialize(object payload) =>
        JsonSerializer.Serialize(payload);
}

public sealed class DapRequest
{
    public int Seq { get; init; }
    public string Command { get; init; } = "";
    public JsonElement? Arguments { get; init; }

    public static DapRequest? TryParse(JsonElement root)
    {
        if (!root.TryGetProperty("command", out var cmd)) return null;
        return new DapRequest
        {
            Seq = root.TryGetProperty("seq", out var seq) ? seq.GetInt32() : 0,
            Command = cmd.GetString() ?? "",
            Arguments = root.TryGetProperty("arguments", out var args) ? args.Clone() : null
        };
    }
}

public sealed class DapResponseBuilder
{
    public object Success(int requestSeq, string command, object? body = null) => new
    {
        type = "response",
        request_seq = requestSeq,
        success = true,
        command,
        body
    };

    public object Error(int requestSeq, string command, string message) => new
    {
        type = "response",
        request_seq = requestSeq,
        success = false,
        command,
        message
    };

    public object Event(string @event, object? body = null) => new
    {
        type = "event",
        @event,
        body
    };
}
