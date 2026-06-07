using System.ComponentModel;
using HlaX64.DebugAdapter;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class DebugCommand : Command<DebugCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Use stdio Debug Adapter Protocol")]
        [CommandOption("--stdio")]
        public bool Stdio { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!settings.Stdio)
        {
            Console.WriteLine("HlaX64 debug adapter — use --stdio for DAP over stdin/stdout.");
            Console.WriteLine("Linux: gdb backend. Windows: lldb (PATH or Program Files/LLVM/bin).");
            return 0;
        }

        var host = new DebugAdapterHost(Console.In, Console.Out);
        host.Run();
        return 0;
    }
}
