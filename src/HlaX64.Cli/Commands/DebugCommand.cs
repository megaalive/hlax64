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
            Console.WriteLine("See rfcs/0023-dap-mvp.md for launch/disconnect MVP.");
            return 0;
        }

        Console.WriteLine(JsonSerializer.Serialize(new
        {
            protocol = "hla64-debug-stub",
            version = Compilation.GetVersion(),
            capabilities = new { launch = true, disconnect = true, breakpoints = false }
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
