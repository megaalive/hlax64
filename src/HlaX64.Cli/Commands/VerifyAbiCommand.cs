using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using HlaX64.Compiler.Verification;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class VerifyAbiCommand : Command<VerifyAbiCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Target triple")]
        [CommandOption("-t|--target")]
        [DefaultValue("linux-x64-sysv")]
        public string Target { get; set; } = "linux-x64-sysv";

        [Description("Output as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
            return ReportFail(settings, $"Source file '{settings.Source}' not found.");

        var options = CliCompilationOptions.FromCli(settings.Target, null, warnBounds: false);
        var sourceText = File.ReadAllText(settings.Source);
        var compile = new Compilation(settings.Source, sourceText, options).Process();
        if (!compile.Success)
            return ReportFail(settings, string.Join('\n', compile.Diagnostics));

        var result = AbiVerifier.Verify(
            options,
            compile.IrFunctions,
            compile.ExternProcedures,
            compile.RecordTypes,
            compile.ProcedureTypes);

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = result.Success,
                version = Compilation.GetVersion(),
                source = Path.GetFullPath(settings.Source),
                target = result.Target,
                externs = result.ExternSymbols,
                procedures = result.Procedures.Select(p => new
                {
                    name = p.Name,
                    returnRegister = p.ReturnRegister,
                    export = p.IsExport,
                    entry = p.IsEntryPoint,
                    parameters = p.Parameters.Select(param => new
                    {
                        name = param.Name,
                        type = param.Type,
                        register = param.Register,
                        stackIndex = param.StackIndex
                    })
                }),
                issues = result.Issues
            });
        }
        else
        {
            Console.WriteLine($"ABI verification — {settings.Source}");
            Console.WriteLine($"Target: {result.Target}");
            if (result.ExternSymbols.Count > 0)
                Console.WriteLine("Externs: " + string.Join(", ", result.ExternSymbols));

            foreach (var p in result.Procedures)
            {
                Console.WriteLine($"  procedure {p.Name} @returns({p.ReturnRegister})");
                foreach (var param in p.Parameters)
                {
                    var loc = param.Register != null
                        ? param.Register
                        : $"stack[{param.StackIndex}]";
                    Console.WriteLine($"    {param.Name}: {param.Type} -> {loc}");
                }
            }

            Console.WriteLine(result.Success ? "OK" : "FAILED");
        }

        return result.Success ? 0 : 1;
    }

    private static int ReportFail(Settings settings, string error)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = false,
                version = Compilation.GetVersion(),
                error
            });
        }
        else
        {
            Console.Error.WriteLine($"Error: {error}");
        }

        return 1;
    }
}
