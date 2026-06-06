using System.ComponentModel;
using HlaX64.Compiler.Debug;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class EmitNasmCommand : Command<EmitNasmCommand.Settings>
{
    public sealed class Settings : CommandSettings, IVerificationCliOptions
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Output NASM file path (default: stdout)")]
        [CommandOption("-o|--output")]
        public string? OutputPath { get; set; }

        [Description("Runtime mode: inline (default) or library")]
        [CommandOption("--runtime")]
        public string? RuntimeMode { get; set; }

        [Description("Target triple: linux-x64-sysv (default) or windows-x64-msabi")]
        [CommandOption("--target")]
        public string? Target { get; set; }

        [Description("Emit source map sidecar (.hlamap.json)")]
        [CommandOption("--source-map")]
        public bool SourceMap { get; set; }

        [Description("Emit DWARF line info stub (Linux only)")]
        [CommandOption("--debug-info")]
        public bool DebugInfo { get; set; }

        [Description("Optimization level: O0 (default) or O1")]
        [CommandOption("--optimize")]
        public string? Optimize { get; set; }

        [Description("CPU baseline profile")]
        [CommandOption("--cpu")]
        public string? Cpu { get; set; }

        [Description("CPU feature toggles (+sse2,-avx2)")]
        [CommandOption("--features")]
        public string[] Features { get; set; } = [];

        [Description("Warn when a literal array index may be out of bounds")]
        [CommandOption("--warn-bounds")]
        public bool WarnBounds { get; set; }

        [CommandOption("--warn-definite")]
        public bool WarnDefinite { get; set; }

        [CommandOption("--warn-unreachable")]
        public bool WarnUnreachable { get; set; }

        [CommandOption("--warn-liveness")]
        public bool WarnLiveness { get; set; }

        [CommandOption("--warn-verify")]
        public bool WarnVerify { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            Console.Error.WriteLine($"Error: Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceText = File.ReadAllText(settings.Source);
        var options = CliCompilationOptions.FromCli(
            settings.Target, settings.RuntimeMode, settings.WarnBounds,
            settings.WarnDefinite, settings.WarnUnreachable, settings.WarnLiveness, settings.WarnVerify,
            settings.Optimize, settings.SourceMap, settings.DebugInfo,
            features: settings.Features, cpu: settings.Cpu);

        try
        {
            var artifacts = CompilePipeline.Compile(settings.Source, sourceText, options);

            if (settings.OutputPath != null)
            {
                File.WriteAllText(settings.OutputPath, artifacts.NasmCode);
                Console.WriteLine($"NASM output written to: {settings.OutputPath}");
            }
            else
            {
                Console.WriteLine(artifacts.NasmCode);
            }

            if (settings.SourceMap && artifacts.SourceMap != null)
            {
                var mapPath = settings.OutputPath != null
                    ? Path.ChangeExtension(settings.OutputPath, ".hlamap.json")
                    : Path.ChangeExtension(settings.Source, ".hlamap.json");
                File.WriteAllText(mapPath, artifacts.SourceMap.ToJson());
                Console.WriteLine($"Source map written to: {mapPath}");
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }
}
