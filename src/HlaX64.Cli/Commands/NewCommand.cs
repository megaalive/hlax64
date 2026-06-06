using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class NewCommand : Command<NewCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<template>")]
        public string Template { get; set; } = "console";

        [CommandArgument(1, "[name]")]
        public string? Name { get; set; }

        [CommandOption("-o|--output")]
        public string? OutputDir { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!settings.Template.Equals("console", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"Unknown template '{settings.Template}'. Supported: console");
            return 1;
        }

        var name = settings.Name ?? "app";
        var dir = settings.OutputDir ?? Path.Combine(Directory.GetCurrentDirectory(), name);
        Directory.CreateDirectory(dir);

        var manifest = $$"""
            name = "{{name}}"
            version = "0.1.0"
            target = "linux-x64-sysv"

            [sources]
            main = "main.hla64"
            """;

        var main = $$"""
            program {{name}};
            begin {{name}};
                call Main();
            end {{name}};

            procedure Main; @returns("rax");
            begin Main;
                mov(42, rax);
            end Main;
            """;

        File.WriteAllText(Path.Combine(dir, "hla64.toml"), manifest);
        File.WriteAllText(Path.Combine(dir, "main.hla64"), main);

        Console.WriteLine($"Created console project in {dir}");
        return 0;
    }
}
