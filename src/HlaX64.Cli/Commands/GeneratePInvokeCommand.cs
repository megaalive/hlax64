using System.ComponentModel;
using HlaX64.Cli.CodeGen;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class GeneratePInvokeCommand : Command<GeneratePInvokeCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Library name for DllImport (default: source filename)")]
        [CommandOption("-l|--library")]
        public string? LibraryName { get; set; }

        [Description("Output .cs file path (default: stdout)")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            Console.Error.WriteLine($"Error: Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceText = File.ReadAllText(settings.Source);
        var libName = settings.LibraryName ?? Path.GetFileNameWithoutExtension(settings.Source);

        try
        {
            var pinvoke = InteropGenerator.GeneratePInvoke(sourceText, libName);

            if (settings.OutputPath != null)
            {
                File.WriteAllText(settings.OutputPath, pinvoke);
                Console.WriteLine($"P/Invoke written to: {settings.OutputPath}");
            }
            else
            {
                Console.WriteLine(pinvoke);
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
