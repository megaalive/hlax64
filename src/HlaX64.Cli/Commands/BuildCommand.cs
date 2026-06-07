using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Cli.Services;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

public sealed class BuildCommand : Command<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings, IVerificationCliOptions
    {
        [Description("Path to the .hla64 source file (optional when hla64.toml is present)")]
        [CommandArgument(0, "[source]")]
        public string? Source { get; set; }

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
        [CommandOption("--warn-bounds")]
        public bool WarnBounds { get; set; }

        [Description("Warn on possible use before definite assignment (HLAX0060)")]
        [CommandOption("--warn-definite")]
        public bool WarnDefinite { get; set; }

        [Description("Warn on unreachable code / missing return path (HLAX0061/62)")]
        [CommandOption("--warn-unreachable")]
        public bool WarnUnreachable { get; set; }

        [Description("Warn when caller-saved registers may be live across call (HLAX0063)")]
        [CommandOption("--warn-liveness")]
        public bool WarnLiveness { get; set; }

        [Description("Enable all Phase 18 verification warnings")]
        [CommandOption("--warn-verify")]
        public bool WarnVerify { get; set; }

        [Description("Emit source map sidecar (.hlamap.json)")]
        [CommandOption("--source-map")]
        public bool SourceMap { get; set; }

        [Description("Emit DWARF line info stub (Linux only)")]
        [CommandOption("--debug-info")]
        public bool DebugInfo { get; set; }

        [Description("Optimization level: O0 (default), O1, or O2")]
        [CommandOption("--optimize")]
        public string? Optimize { get; set; }

        [Description("Emit proof bundle directory")]
        [CommandOption("--proof-bundle")]
        public bool ProofBundle { get; set; }

        [Description("Include tests.json summary in proof bundle when present")]
        [CommandOption("--proof-bundle-include-tests")]
        public bool ProofBundleIncludeTests { get; set; }

        [Description("CPU baseline profile")]
        [CommandOption("--cpu")]
        public string? Cpu { get; set; }

        [Description("CPU feature toggles (+sse2,-avx2)")]
        [CommandOption("--features")]
        public string[] Features { get; set; } = [];
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var (sourceFile, sourceText, projectDir) = ResolveSource(settings);
        if (sourceFile == null)
        {
            var (_, _, _, lockError) = ProjectBuildHelper.ResolveProjectSource(settings.Source, requireLock: true);
            return Fail(settings, null, null, null, null, null, lockError ?? "No source file or hla64.toml manifest found.");
        }

        if (!File.Exists(sourceFile))
            return Fail(settings, settings.Source, null, null, null, null, $"Source file '{sourceFile}' not found.");

        sourceFile = Path.GetFullPath(sourceFile);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var outputDir = settings.OutputDir ?? Path.Combine(projectDir ?? Path.GetDirectoryName(sourceFile)!, "build", sourceName);
        outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputDir);

        var options = CliCompilationOptions.FromCli(
            settings.Target, settings.RuntimeMode, settings.WarnBounds,
            settings.WarnDefinite, settings.WarnUnreachable, settings.WarnLiveness, settings.WarnVerify,
            settings.Optimize, settings.SourceMap, settings.DebugInfo,
            features: settings.Features, cpu: settings.Cpu);
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

            var artifacts = CompilePipeline.Compile(sourceFile, sourceText, options);
            var compileResult = artifacts.Result;
            File.WriteAllText(nasmFile, artifacts.NasmCode);
            if (!settings.Json)
                Console.WriteLine($"  -> {nasmFile}");

            if (settings.SourceMap && artifacts.SourceMap != null)
            {
                var mapFile = Path.Combine(outputDir, $"{sourceName}.hlamap.json");
                File.WriteAllText(mapFile, artifacts.SourceMap.ToJson());
                if (!settings.Json)
                    Console.WriteLine($"  -> {mapFile}");
            }

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

            string? proofBundleDir = null;
            if (settings.ProofBundle)
            {
                proofBundleDir = ProofBundleWriter.Write(
                    sourceFile, sourceText, options, outputDir, artifacts, nasmFile, objFile, outputFile,
                    includeTestsSummary: settings.ProofBundleIncludeTests,
                    projectDir: projectDir);
                if (!settings.Json)
                    Console.WriteLine($"  -> proof bundle: {proofBundleDir}");
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
                    outputFile,
                    sourceMapFile = settings.SourceMap ? Path.Combine(outputDir, $"{sourceName}.hlamap.json") : null,
                    proofBundleDir
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

    private static (string? SourceFile, string SourceText, string? ProjectDir) ResolveSource(Settings settings)
    {
        var (sourceFile, sourceText, projectDir, error) =
            ProjectBuildHelper.ResolveProjectSource(settings.Source, requireLock: true);
        if (error != null && sourceFile == null)
            return (null, "", projectDir);
        return (sourceFile, sourceText, projectDir);
    }
}
