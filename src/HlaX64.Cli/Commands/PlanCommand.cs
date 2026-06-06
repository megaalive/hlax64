using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class PlanCommand : Command<PlanCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [CommandOption("--target")]
        public string? Target { get; set; }

        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
        {
            Report(settings, false, error: $"Source file '{settings.Source}' not found.");
            return 1;
        }

        var sourceFile = Path.GetFullPath(settings.Source);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var target = settings.Target ?? "linux-x64-sysv";
        var outputDir = Path.Combine(Path.GetDirectoryName(sourceFile)!, "build", sourceName);
        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outputDir, $"{sourceName}.o");
        var exeFile = Path.Combine(outputDir, sourceName);

        var plan = new
        {
            target,
            compilerVersion = Compilation.GetVersion(),
            source = sourceFile,
            toolchain = new[]
            {
                new { step = "compile", command = $"hla64 emit-nasm \"{sourceFile}\" -o \"{nasmFile}\"" },
                new { step = "assemble", command = $"nasm -f elf64 \"{nasmFile}\" -o \"{objFile}\"" },
                new { step = "link", command = $"ld \"{objFile}\" -o \"{exeFile}\"" }
            },
            artifacts = new[] { nasmFile, objFile, exeFile },
            nasmAvailable = NasmTool.TryFindNasm(out _)
        };

        Report(settings, true, plan, sourceFile, target, nasmFile, objFile, exeFile);
        return 0;
    }

    private static void Report(Settings settings, bool success, object? plan = null, string? sourceFile = null,
        string? target = null, string? nasmFile = null, string? objFile = null, string? exeFile = null, string? error = null)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success,
                version = Compilation.GetVersion(),
                plan,
                error
            });
            return;
        }

        if (!success)
        {
            Console.Error.WriteLine($"Error: {error}");
            return;
        }

        Console.WriteLine($"Compilation plan for {sourceFile}:");
        Console.WriteLine($"  target: {target}");
        Console.WriteLine($"  emit-nasm -> {nasmFile}");
        Console.WriteLine($"  assemble -> {objFile}");
        Console.WriteLine($"  link -> {exeFile}");
    }
}
