using System.Text;
using System.Text.Json;

namespace HlaX64.LanguageServer;

/// <summary>
/// Minimal LSP server (diagnostics only) over stdio.
/// </summary>
internal static class Program
{
    private static readonly Dictionary<string, string> Documents = new();

    public static void Main()
    {
        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = Encoding.UTF8;

        while (true)
        {
            var request = ReadMessage();
            if (request == null) break;

            JsonElement root;
            try
            {
                root = JsonSerializer.Deserialize<JsonElement>(request);
            }
            catch
            {
                continue;
            }

            if (!root.TryGetProperty("method", out var methodEl)) continue;
            var method = methodEl.GetString();
            var id = root.TryGetProperty("id", out var idEl) ? idEl : default;
            var hasId = id.ValueKind != JsonValueKind.Undefined;

            switch (method)
            {
                case "initialize" when hasId:
                    WriteResponse(id, new
                    {
                        capabilities = new
                        {
                            textDocumentSync = 1,
                            hoverProvider = true,
                            completionProvider = new { triggerCharacters = new[] { ".", "[", "&" } },
                            definitionProvider = true,
                            documentSymbolProvider = true,
                            documentFormattingProvider = true,
                            executeCommandProvider = new { commands = new[] { "hla64.showIr", "hla64.showNasm", "hla64.showStackLayout" } }
                        },
                        serverInfo = new { name = "HlaX64.LanguageServer", version = HlaX64.Compiler.Compilation.GetVersion() }
                    });
                    break;

                case "initialized":
                case "exit":
                    break;

                case "shutdown" when hasId:
                    WriteResponse(id, new { });
                    return;

                case "textDocument/didOpen":
                    if (root.TryGetProperty("params", out var openParams))
                    {
                        var uri = openParams.GetProperty("textDocument").GetProperty("uri").GetString()!;
                        var text = openParams.GetProperty("textDocument").GetProperty("text").GetString() ?? "";
                        Documents[uri] = text;
                        DocumentDiagnostics.Publish(uri, text, SendNotification);
                    }
                    break;

                case "textDocument/didChange":
                    if (root.TryGetProperty("params", out var changeParams))
                    {
                        var uri = changeParams.GetProperty("textDocument").GetProperty("uri").GetString()!;
                        var change = changeParams.GetProperty("contentChanges")[0];
                        var text = change.GetProperty("text").GetString() ?? "";
                        Documents[uri] = text;
                        DocumentDiagnostics.Publish(uri, text, SendNotification);
                    }
                    break;

                case "textDocument/didClose":
                    if (root.TryGetProperty("params", out var closeParams))
                    {
                        var uri = closeParams.GetProperty("textDocument").GetProperty("uri").GetString()!;
                        Documents.Remove(uri);
                    }
                    break;

                case "textDocument/hover" when hasId:
                    HandleHover(id, root);
                    break;

                case "textDocument/completion" when hasId:
                    HandleCompletion(id, root);
                    break;

                case "textDocument/definition" when hasId:
                    HandleDefinition(id, root);
                    break;

                case "textDocument/documentSymbol" when hasId:
                    HandleDocumentSymbol(id, root);
                    break;

                case "textDocument/formatting" when hasId:
                    HandleFormatting(id, root);
                    break;

                case "workspace/executeCommand" when hasId:
                    HandleExecuteCommand(id, root);
                    break;
            }
        }
    }

    private static void HandleExecuteCommand(JsonElement id, JsonElement root)
    {
        if (!root.TryGetProperty("params", out var p))
        {
            WriteResponse(id, null!);
            return;
        }

        var command = p.GetProperty("command").GetString() ?? "";
        var args = p.GetProperty("arguments");
        var uri = args[0].GetString() ?? "";
        Documents.TryGetValue(uri, out var text);
        WriteResponse(id, LanguageServerEditorServices.ExecuteCommand(command, text ?? "", uri)!);
    }

    private static void HandleDefinition(JsonElement id, JsonElement root)
    {
        if (!TryGetDocumentPosition(root, out var uri, out var line, out var character, out var text))
        {
            WriteResponse(id, null!);
            return;
        }

        WriteResponse(id, LanguageServerEditorServices.GetDefinition(text, line, character, uri)!);
    }

    private static void HandleDocumentSymbol(JsonElement id, JsonElement root)
    {
        if (!root.TryGetProperty("params", out var p))
        {
            WriteResponse(id, Array.Empty<object>());
            return;
        }

        var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
        Documents.TryGetValue(uri, out var text);
        WriteResponse(id, LanguageServerEditorServices.GetDocumentSymbols(text ?? ""));
    }

    private static void HandleFormatting(JsonElement id, JsonElement root)
    {
        if (!root.TryGetProperty("params", out var p))
        {
            WriteResponse(id, null!);
            return;
        }

        var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
        if (!Documents.TryGetValue(uri, out var text))
        {
            WriteResponse(id, null!);
            return;
        }

        WriteResponse(id, LanguageServerEditorServices.FormatDocument(text)!);
    }

    private static bool TryGetDocumentPosition(JsonElement root, out string uri, out int line, out int character, out string text)
    {
        uri = "";
        line = 0;
        character = 0;
        text = "";
        if (!root.TryGetProperty("params", out var p))
            return false;

        uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
        var pos = p.GetProperty("position");
        line = pos.GetProperty("line").GetInt32();
        character = pos.GetProperty("character").GetInt32();
        return Documents.TryGetValue(uri, out text!);
    }

    private static void HandleHover(JsonElement id, JsonElement root)
    {
        if (!root.TryGetProperty("params", out var p))
        {
            WriteResponse(id, null!);
            return;
        }

        var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
        var pos = p.GetProperty("position");
        var line = pos.GetProperty("line").GetInt32();
        var character = pos.GetProperty("character").GetInt32();
        if (!Documents.TryGetValue(uri, out var text))
        {
            WriteResponse(id, null!);
            return;
        }

        WriteResponse(id, LanguageServerEditorServices.GetHover(text, line, character)!);
    }

    private static void HandleCompletion(JsonElement id, JsonElement root)
    {
        if (!root.TryGetProperty("params", out var p))
        {
            WriteResponse(id, new { items = Array.Empty<object>() });
            return;
        }

        var uri = p.GetProperty("textDocument").GetProperty("uri").GetString()!;
        var pos = p.GetProperty("position");
        var line = pos.GetProperty("line").GetInt32();
        var character = pos.GetProperty("character").GetInt32();
        Documents.TryGetValue(uri, out var text);
        WriteResponse(id, LanguageServerEditorServices.GetCompletions(text ?? "", line, character));
    }

    private static void SendNotification(object payload)
    {
        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });
        WriteRaw(json);
    }

    private static void WriteResponse(JsonElement id, object result)
    {
        var response = new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = id,
            ["result"] = result
        };
        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        WriteRaw(json);
    }

    private static void WriteRaw(string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        var header = $"Content-Length: {bytes.Length}\r\n\r\n";
        var stdout = Console.OpenStandardOutput();
        stdout.Write(Encoding.UTF8.GetBytes(header));
        stdout.Write(bytes);
        stdout.Flush();
    }

    private static string? ReadMessage()
    {
        var stdin = Console.OpenStandardInput();
        int contentLength = -1;

        while (true)
        {
            var line = ReadLine(stdin);
            if (line == null) return null;
            if (line.Length == 0) break;
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                contentLength = int.Parse(line["Content-Length:".Length..].Trim());
        }

        if (contentLength <= 0) return null;
        var buffer = new byte[contentLength];
        var offset = 0;
        while (offset < contentLength)
        {
            var read = stdin.Read(buffer, offset, contentLength - offset);
            if (read <= 0) return null;
            offset += read;
        }

        return Encoding.UTF8.GetString(buffer);
    }

    private static string? ReadLine(Stream stream)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var b = stream.ReadByte();
            if (b == -1) return sb.Length > 0 ? sb.ToString() : null;
            if (b == '\r') continue;
            if (b == '\n') return sb.ToString();
            sb.Append((char)b);
        }
    }
}
