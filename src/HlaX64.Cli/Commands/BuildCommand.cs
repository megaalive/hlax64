using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class BuildCommand : Command<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Path to the .hla64 source file")]
        [CommandArgument(0, "<source>")]
        public string Source { get; set; } = string.Empty;

        [Description("Output directory (default: build/)")]
        [CommandOption("-o|--output")]
        public string? OutputDir { get; set; }

        [Description("Runtime mode: inline (default) or library")]
        [CommandOption("--runtime")]
        public string? RuntimeMode { get; set; }

        [Description("Output kind: executable (default) or shared-library")]
        [CommandOption("--output-kind")]
        public string? OutputKind { get; set; }

        [Description("Target triple: linux-x64-sysv (default) or windows-x64-msabi")]
        [CommandOption("--target")]
        public string? Target { get; set; }

        [Description("Output build result as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }

        [Description("Warn when a literal array index may be out of bounds")]
        [CommandOption("-Wbounds|--warn-bounds")]
        public bool WarnBounds { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        if (!File.Exists(settings.Source))
            return Fail(settings, settings.Source, null, null, null, null, $"Source file '{settings.Source}' not found.");

        var sourceFile = Path.GetFullPath(settings.Source);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var outputDir = settings.OutputDir ?? Path.Combine(Path.GetDirectoryName(sourceFile)!, "..", "build", sourceName);
        outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputDir);

        var options = CliCompilationOptions.FromCli(settings.Target, settings.RuntimeMode, settings.WarnBounds);
        var targetTriple = options.Target;

        bool isWindows = options.Target.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase);
        bool isShared = settings.OutputKind?.ToLowerInvariant() == "shared-library";
        string nasmFormat = isWindows ? "win64" : "elf64";
        string objExt = isWindows ? ".obj" : ".o";
        string ext = isShared ? (isWindows ? ".dll" : ".so") : (isWindows ? ".exe" : "");
        string libPrefix = isShared ? "lib" : "";

        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outputDir, $"{sourceName}{objExt}");
        var outputFile = Path.Combine(outputDir, $"{libPrefix}{sourceName}{ext}");
        var targetName = settings.Target ?? "linux-x64-sysv";
        var outputKind = settings.OutputKind ?? "executable";

        try
        {
            if (!settings.Json)
                Console.WriteLine($"Compiling {sourceFile}...");

            var sourceText = File.ReadAllText(sourceFile);
            var compileResult = CompilePipeline.Process(sourceFile, sourceText, options);
            if (!compileResult.Success)
                throw new InvalidOperationException(string.Join("\n", compileResult.Diagnostics));

            var emitter = new HlaX64.Backend.Nasm.Emitters.NasmEmitter();
            var nasmCode = emitter.Emit(compileResult.LoweredFunctions, compileResult.StringLiterals, compileResult.GlobalData);
            File.WriteAllText(nasmFile, nasmCode);
            if (!settings.Json)
                Console.WriteLine($"  -> {nasmFile}");

            if (!settings.Json)
                Console.WriteLine("Assembling with NASM...");
            if (!NasmTool.TryFindNasm(out var nasmPath))
                return Fail(settings, sourceFile, targetName, outputKind, nasmFile, null,
                    "NASM not found. Install NASM (https://nasm.us)");

            if (!settings.Json)
                Console.WriteLine($"  NASM: {nasmPath}");

            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: nasmFormat))
                return Fail(settings, sourceFile, targetName, outputKind, nasmFile, objFile, nasmError);

            if (!settings.Json)
                Console.WriteLine($"  -> {objFile}");

            if (!settings.Json)
                Console.WriteLine(isShared ? "Linking shared library..." : "Linking...");

            bool linkSuccess;
            bool requiresWslRun = false;
            string linkError = "";
            if (isWindows)
                linkSuccess = LinkerTool.TryLinkWindows(objFile, outputFile, out linkError, shared: isShared,
                    extraLibraries: compileResult.LinkLibraries);
            else
                linkSuccess = LinkerTool.TryLink(objFile, outputFile, out linkError, out requiresWslRun,
                    shared: isShared, extraLibraries: compileResult.LinkLibraries);

            if (!linkSuccess)
                return Fail(settings, sourceFile, targetName, outputKind, nasmFile, objFile, linkError);

            if (!settings.Json)
                Console.WriteLine($"  -> {outputFile}");

            if (!isWindows && !requiresWslRun && !isShared)
            {
                try
                {
                    var chmod = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{outputFile}\"",
                        UseShellExecute = false
                    };
                    System.Diagnostics.Process.Start(chmod)?.WaitForExit(2000);
                }
                catch { }
            }

            if (settings.Json)
            {
                CliJson.Write(new
                {
                    schemaVersion = CliJson.SchemaVersion,
                    success = true,
                    version = Compilation.GetVersion(),
                    source = sourceFile,
                    target = targetName,
                    outputKind,
                    nasmFile,
                    objectFile = objFile,
                    outputFile
                });
            }
            else
            {
                Console.WriteLine($"\nBuild successful: {outputFile}");
            }

            return 0;
        }
        catch (InvalidOperationException ex)
        {
            return Fail(settings, sourceFile, targetName, outputKind, nasmFile, objFile, ex.Message);
        }
        catch (Exception ex)
        {
            return Fail(settings, sourceFile, targetName, outputKind, nasmFile, objFile, ex.Message);
        }
    }

    private static int Fail(Settings settings, string? source, string? target, string? outputKind,
        string? nasmFile, string? objFile, string error)
    {
        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = false,
                version = Compilation.GetVersion(),
                source,
                target,
                outputKind,
                nasmFile,
                objectFile = objFile,
                error
            });
        }
        else
        {
            Console.Error.WriteLine(error.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ? error : $"Error: {error}");
            if (nasmFile != null && error.Contains("NASM", StringComparison.OrdinalIgnoreCase))
                Console.Error.WriteLine($"  NASM output saved at: {nasmFile}");
        }

        return 1;
    }
}
