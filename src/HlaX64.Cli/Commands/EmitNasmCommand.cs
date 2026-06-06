using System.ComponentModel;
using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class EmitNasmCommand : Command<EmitNasmCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Output NASM file path (default: stdout)")]
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

        try
        {
            // Lex
            var lexer = new Lexer(sourceText);
            var tokens = lexer.Tokenize();

            // Parse
            var parser = new Parser(tokens);
            var program = parser.Parse();

            // Emit NASM
            var emitter = new NasmEmitter();
            var nasmCode = emitter.Emit(program);
            var dataSection = emitter.GenerateDataSection();

            var output = nasmCode + dataSection;

            if (settings.OutputPath != null)
            {
                File.WriteAllText(settings.OutputPath, output);
                Console.WriteLine($"NASM output written to: {settings.OutputPath}");
            }
            else
            {
                Console.WriteLine(output);
            }

            return 0;
        }
        catch (ParseException ex)
        {
            Console.Error.WriteLine($"Parse error: {ex.Message}");
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}