using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using Spectre.Console.Cli;
using System.Text.Json;

namespace HlaX64.Cli.Commands;

public sealed class DebugCommand : Command<DebugCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Use stdio JSON debug protocol (DAP MVP stub)")]
        [CommandOption("--stdio")]
        public bool Stdio { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!settings.Stdio)
        {
            Console.WriteLine("HlaX64 debug adapter stub — use --stdio for JSON protocol.");
            Console.WriteLine("See rfcs/0023-dap-mvp.md for launch/disconnect/setBreakpoint/stackTrace MVP.");
            return 0;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            protocol = "hla64-debug-stub",
            version = Compilation.GetVersion(),
            capabilities = new { launch = true, disconnect = true, breakpoints = true, stackTrace = true }
        }));

        string? line;
        while ((line = Console.ReadLine()) != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var cmd = doc.RootElement.GetProperty("command").GetString();
                object response = cmd switch
                {
                    "launch" => new { type = "response", command = "launch", success = true },
                    "disconnect" => new { type = "response", command = "disconnect", success = true },
                    "setBreakpoint" => new
                    {
                        type = "response",
                        command = "setBreakpoint",
                        success = true,
                        body = new
                        {
                            breakpoints = new[]
                            {
                                new
                                {
                                    verified = true,
                                    id = doc.RootElement.TryGetProperty("arguments", out var args) &&
                                          args.TryGetProperty("line", out var ln)
                                        ? ln.GetInt32()
                                        : 1
                                }
                            }
                        }
                    },
                    "stackTrace" => new
                    {
                        type = "response",
                        command = "stackTrace",
                        success = true,
                        body = new
                        {
                            stackFrames = new[]
                            {
                                new { id = 1, name = "_start", line = 1, column = 1, source = new { path = "main.hla64" } }
                            },
                            totalFrames = 1
                        }
                    },
                    _ => new { type = "response", command = cmd, success = false, message = "unsupported" }
                };
                Console.WriteLine(JsonSerializer.Serialize(response));
                if (cmd == "disconnect") break;
            }
            catch
            {
                Console.WriteLine(JsonSerializer.Serialize(new { type = "error", message = "invalid json" }));
            }
        }

        return 0;
    }
}
