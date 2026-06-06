using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Cpu;
using Spectre.Console.Cli;
using System.Text.Json;

namespace HlaX64.Cli.Commands;

public sealed class ListInstructionsCommand : Command<ListInstructionsCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var db = InstructionDatabase.LoadDefault();
        var items = db.All.Select(i => new
        {
            mnemonic = i.Mnemonic,
            category = i.Category,
            minOps = i.MinOps,
            maxOps = i.MaxOps,
            features = i.Features
        }).ToList();

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = true,
                version = Compilation.GetVersion(),
                count = items.Count,
                instructions = items
            });
            return 0;
        }

        foreach (var i in items)
        {
            var feat = i.features.Count > 0 ? $" [{string.Join(',', i.features)}]" : "";
            Console.WriteLine($"{i.mnemonic,-10} {i.category}{feat}");
        }

        return 0;
    }
}
