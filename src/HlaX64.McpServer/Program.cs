using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HlaX64.McpServer.Tools;
using ModelContextProtocol.Server;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

var toolType = typeof(HlaX64Tools);
var toolMethods = toolType
    .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
    .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() != null)
    .ToList();

var instance = Activator.CreateInstance(toolType);
var jsonOpts = new JsonSerializerOptions { WriteIndented = true };
var noId = new JsonElement();

while (true)
{
    var request = ReadRequest();
    if (request == null) break;

    JsonElement root;
    try
    {
        root = JsonSerializer.Deserialize<JsonElement>(request);
    }
    catch
    {
        WriteError(noId, -32700, "Parse error");
        continue;
    }

    var id = root.TryGetProperty("id", out var idEl) ? idEl : noId;
    var method = root.TryGetProperty("method", out var mEl) ? mEl.GetString() : null;

    if (method == null)
    {
        WriteError(id, -32600, "Invalid Request: missing method");
        continue;
    }

    if (method == "tools/list")
    {
        var tools = new List<object>();
        foreach (var mi in toolMethods)
        {
            var desc = mi.GetCustomAttribute<DescriptionAttribute>()?.Description ?? mi.Name;
            var properties = new Dictionary<string, object>();
            var required = new List<string>();

            foreach (var param in mi.GetParameters())
            {
                var pName = param.Name!;
                var pDesc = $"Parameter {pName}";
                var isOptional = param.HasDefaultValue;

                var pType = param.ParameterType;
                var underlying = Nullable.GetUnderlyingType(pType) ?? pType;

                string jsonType;
                if (underlying == typeof(int) || underlying == typeof(long))
                    jsonType = "integer";
                else if (underlying == typeof(bool))
                    jsonType = "boolean";
                else if (underlying == typeof(double) || underlying == typeof(float))
                    jsonType = "number";
                else
                    jsonType = "string";

                properties[pName] = new Dictionary<string, object>
                {
                    ["type"] = jsonType,
                    ["description"] = pDesc
                };

                if (!isOptional)
                    required.Add(pName);
            }

            var inputSchema = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties
            };
            if (required.Count > 0)
                inputSchema["required"] = required;

            tools.Add(new
            {
                name = mi.Name,
                description = desc,
                inputSchema
            });
        }

        WriteResponse(id, new { tools = tools.ToArray() });
    }
    else if (method == "tools/call")
    {
        var name = root.GetProperty("params").GetProperty("name").GetString();
        var arguments = root.GetProperty("params").TryGetProperty("arguments", out var argsEl)
            ? argsEl : default;

        var mi = toolMethods.FirstOrDefault(m =>
            m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (mi == null)
        {
            WriteError(id, -32602, $"Unknown tool: {name}");
            continue;
        }

        try
        {
            var callArgs = new List<object?>();
            foreach (var param in mi.GetParameters())
            {
                var pName = param.Name!;
                var pType = param.ParameterType;
                var isNullable = pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(Nullable<>);
                var baseType = isNullable ? Nullable.GetUnderlyingType(pType) : pType;

                if (arguments.ValueKind != JsonValueKind.Undefined &&
                    arguments.TryGetProperty(pName, out var argVal))
                {
                    if (baseType == typeof(string))
                        callArgs.Add(argVal.GetString());
                    else if (baseType == typeof(int))
                        callArgs.Add(argVal.GetInt32());
                    else if (baseType == typeof(bool))
                        callArgs.Add(argVal.GetBoolean());
                    else if (baseType == typeof(long))
                        callArgs.Add(argVal.GetInt64());
                    else if (baseType == typeof(double))
                        callArgs.Add(argVal.GetDouble());
                    else if (baseType == typeof(float))
                        callArgs.Add(argVal.GetSingle());
                    else
                        callArgs.Add(argVal.GetString());
                }
                else if (param.HasDefaultValue)
                {
                    callArgs.Add(param.DefaultValue);
                }
                else
                {
                    callArgs.Add(baseType!.IsValueType ? Activator.CreateInstance(baseType) : null);
                }
            }

            var result = mi.Invoke(instance, callArgs.ToArray());

            WriteResponse(id, new
            {
                content = new[]
                {
                    new
                    {
                        type = "text",
                        text = result?.ToString() ?? ""
                    }
                }
            });
        }
        catch (Exception ex)
        {
            WriteError(id, -32603, $"Internal error: {ex.InnerException?.Message ?? ex.Message}");
        }
    }
    else if (method == "ping")
    {
        WriteResponse(id, new { });
    }
    else if (method == "initialize")
    {
        WriteResponse(id, new
        {
            protocolVersion = "2024-11-05",
            capabilities = new
            {
                tools = new { }
            },
            serverInfo = new
            {
                name = "HlaX64.McpServer",
                version = "1.0.0"
            }
        });
    }
    else if (method == "notifications/initialized")
    {
    }
    else
    {
        WriteError(id, -32601, $"Method not found: {method}");
    }
}

static string? ReadRequest()
{
    var stdin = Console.OpenStandardInput();
    var headerBuilder = new StringBuilder();

    while (true)
    {
        var line = ReadLine(stdin);
        if (line == null) return null;
        if (line.Length == 0) break;
        headerBuilder.AppendLine(line);
    }

    var header = headerBuilder.ToString();
    var match = Regex.Match(header, @"Content-Length:\s*(\d+)", RegexOptions.IgnoreCase);
    if (!match.Success) return null;

    var length = int.Parse(match.Groups[1].Value);
    var buffer = new byte[length];
    var offset = 0;
    while (offset < length)
    {
        var read = stdin.Read(buffer, offset, length - offset);
        if (read <= 0) return null;
        offset += read;
    }

    return Encoding.UTF8.GetString(buffer);
}

static string? ReadLine(Stream stream)
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

static void WriteResponse(JsonElement id, object result)
{
    var response = new Dictionary<string, object?>
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.ValueKind == JsonValueKind.Undefined ? null : id,
        ["result"] = result
    };
    WriteMessage(response);
}

static void WriteError(JsonElement id, int code, string message)
{
    var response = new Dictionary<string, object?>
    {
        ["jsonrpc"] = "2.0",
        ["id"] = id.ValueKind == JsonValueKind.Undefined ? null : id,
        ["error"] = new { code, message }
    };
    WriteMessage(response);
}

static void WriteMessage(object response)
{
    var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });
    var bytes = Encoding.UTF8.GetBytes(json);
    var header = $"Content-Length: {bytes.Length}\r\n\r\n";

    var stdout = Console.OpenStandardOutput();
    var headerBytes = Encoding.UTF8.GetBytes(header);
    stdout.Write(headerBytes, 0, headerBytes.Length);
    stdout.Write(bytes, 0, bytes.Length);
    stdout.Flush();
}
