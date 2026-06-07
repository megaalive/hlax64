using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using HlaX64.AssemblyLab.Models;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Services;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Analysis;
using HlaX64.Compiler.Debug;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Options;

namespace HlaX64.AssemblyLab.Services;

public sealed record LabCompileResult(
    bool Success,
    IReadOnlyList<LabDiagnosticItem> Diagnostics,
    string? IrText,
    string? NasmText,
    string? AbiText,
    SourceMapDocument? SourceMap);

public sealed record LabBuildResult(
    bool Success,
    string Message,
    string? OutputFile,
    string? NasmFile,
    string? SourceMapFile,
    string? ProofBundleDir,
    SourceMapDocument? SourceMap,
    string? NasmText);

public sealed record LabRunResult(
    bool Success,
    int ExitCode,
    string Stdout,
    string Stderr,
    string Message);

public sealed class AssemblyLabBackend
{
    public static readonly string[] TargetChoices =
    [
        "linux-x64-sysv",
        "windows-x64-msabi"
    ];

    public static string ResolveDefaultTarget()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (LinkerTool.TryFindWindowsLinker(out _, out _, out _))
                return "windows-x64-msabi";
            if (LinkerTool.TryFindLinker(out _, out _, out _))
                return "linux-x64-sysv";
        }

        return "linux-x64-sysv";
    }

    public LabCompileResult Compile(string sourcePath, string sourceText, string target = "linux-x64-sysv")
    {
        var options = BuildOptions(target, emitSourceMap: true);
        var report = ExplainReport.Create(sourcePath, sourceText, options);
        var diagnostics = ToDiagnosticItems(report.StructuredDiagnostics);

        string? irText = null;
        string? abiText = null;
        SourceMapDocument? sourceMap = null;

        if (report.Success)
        {
            irText = FormatIr(report);
            abiText = FormatAbi(report, options, sourceText);
            try
            {
                var artifacts = CompilePipeline.Compile(sourcePath, sourceText, options);
                sourceMap = artifacts.SourceMap;
            }
            catch
            {
                // explain succeeded; map is optional for live compile
            }
        }

        return new LabCompileResult(
            report.Success,
            diagnostics,
            irText,
            report.Nasm,
            abiText,
            sourceMap);
    }

    public LabBuildResult Build(
        string sourcePath,
        string sourceText,
        string target,
        string? outputDir = null,
        bool proofBundle = false)
    {
        var options = BuildOptions(target, emitSourceMap: true);
        var sourceName = Path.GetFileNameWithoutExtension(sourcePath);
        outputDir ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, "build", sourceName);
        outputDir = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputDir);

        var isWindows = options.Target.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase);
        var nasmFormat = isWindows ? "win64" : "elf64";
        var objExt = isWindows ? ".obj" : ".o";
        var ext = isWindows ? ".exe" : "";
        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outputDir, $"{sourceName}{objExt}");
        var outputFile = Path.Combine(outputDir, $"{sourceName}{ext}");
        var mapFile = Path.Combine(outputDir, $"{sourceName}.hlamap.json");

        try
        {
            var artifacts = CompilePipeline.Compile(sourcePath, sourceText, options);
            File.WriteAllText(nasmFile, artifacts.NasmCode);

            if (artifacts.SourceMap != null)
                File.WriteAllText(mapFile, artifacts.SourceMap.ToJson());

            if (!NasmTool.TryFindNasm(out var nasmPath))
                return new LabBuildResult(false, "NASM not found. Install NASM (https://nasm.us)", null, nasmFile, null, null, artifacts.SourceMap, artifacts.NasmCode);

            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: nasmFormat))
                return new LabBuildResult(false, nasmError, null, nasmFile, mapFile, null, artifacts.SourceMap, artifacts.NasmCode);

            bool linkSuccess;
            string linkError = "";
            if (isWindows)
                linkSuccess = LinkerTool.TryLinkWindows(objFile, outputFile, out linkError,
                    extraLibraries: artifacts.Result.LinkLibraries);
            else
                linkSuccess = LinkerTool.TryLink(objFile, outputFile, out linkError, out _,
                    extraLibraries: artifacts.Result.LinkLibraries);

            string? proofDir = null;
            if (proofBundle)
            {
                proofDir = ProofBundleWriter.Write(
                    sourcePath, sourceText, options, outputDir, artifacts, nasmFile, objFile, outputFile,
                    compileOnly: !linkSuccess);
            }

            if (!linkSuccess)
            {
                if (proofBundle && proofDir != null)
                {
                    return new LabBuildResult(
                        true,
                        $"Proof bundle exported (compile-only; linking skipped):\n{linkError}",
                        null,
                        nasmFile,
                        File.Exists(mapFile) ? mapFile : null,
                        proofDir,
                        artifacts.SourceMap,
                        artifacts.NasmCode);
                }

                return new LabBuildResult(false, linkError, null, nasmFile, mapFile, null, artifacts.SourceMap, artifacts.NasmCode);
            }

            if (!isWindows)
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "chmod",
                        Arguments = $"+x \"{outputFile}\"",
                        UseShellExecute = false
                    })?.WaitForExit(2000);
                }
                catch { /* optional on non-Unix hosts */ }
            }

            return new LabBuildResult(
                true,
                proofBundle && proofDir != null
                    ? $"Build and proof bundle successful: {outputFile}"
                    : $"Build successful: {outputFile}",
                outputFile,
                nasmFile,
                File.Exists(mapFile) ? mapFile : null,
                proofDir,
                artifacts.SourceMap,
                artifacts.NasmCode);
        }
        catch (InvalidOperationException ex)
        {
            return new LabBuildResult(false, ex.Message, null, nasmFile, null, null, null, null);
        }
        catch (Exception ex)
        {
            return new LabBuildResult(false, ex.Message, null, nasmFile, null, null, null, null);
        }
    }

    public LabRunResult Run(string sourcePath, string sourceText, string target, string? builtOutputFile = null)
    {
        var build = Build(sourcePath, sourceText, target);
        if (!build.Success || build.OutputFile == null)
            return new LabRunResult(false, 1, "", "", build.Message);

        var exeFile = builtOutputFile ?? build.OutputFile;
        var isWindows = target.Contains("windows", StringComparison.OrdinalIgnoreCase);

        try
        {
            ProcessStartInfo psi;
            var requiresWsl = !isWindows
                && RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                && File.Exists(exeFile);

            if (requiresWsl)
            {
                var wslPath = LinkerTool.ToWslPath(exeFile);
                psi = new ProcessStartInfo
                {
                    FileName = "wsl",
                    Arguments = wslPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
            }
            else
            {
                psi = new ProcessStartInfo
                {
                    FileName = exeFile,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
            }

            using var process = Process.Start(psi);
            if (process == null)
                return new LabRunResult(false, 1, "", "", "Failed to start process.");

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new LabRunResult(
                true,
                process.ExitCode,
                stdout,
                stderr,
                $"Program exited with code {process.ExitCode}");
        }
        catch (Exception ex)
        {
            return new LabRunResult(false, 1, "", "", ex.Message);
        }
    }

    public LabBuildResult ExportProofBundle(string sourcePath, string sourceText, string target, string? outputDir = null)
        => Build(sourcePath, sourceText, target, outputDir, proofBundle: true);

    public CapabilityManifest AnalyzeCapabilities(string sourceText)
    {
        try
        {
            return CapabilityAnalyzer.Analyze(sourceText);
        }
        catch
        {
            return new CapabilityManifest();
        }
    }

    public string SummarizeCapabilities(CapabilityManifest manifest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"filesystemAccess: {manifest.FilesystemAccess}");
        sb.AppendLine($"hasStdoutPut: {manifest.HasStdoutPut}");
        sb.AppendLine($"hasExtern: {manifest.HasExtern}");
        sb.AppendLine($"syscalls: [{string.Join(", ", manifest.Syscalls)}]");
        sb.AppendLine($"externalLibraries: [{string.Join(", ", manifest.ExternalLibraries)}]");
        sb.AppendLine($"externProcedures: [{string.Join(", ", manifest.ExternProcedures)}]");
        return sb.ToString().TrimEnd();
    }

    public SourceMapDocument? LoadSourceMap(string mapPath)
    {
        if (!File.Exists(mapPath))
            return null;
        var json = File.ReadAllText(mapPath);
        return JsonSerializer.Deserialize<SourceMapDocument>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    public int? FindNasmLineForSource(SourceMapDocument? map, int sourceLine)
        => map?.LookupBySource(sourceLine)?.NasmLine;

    public string HighlightNasmLine(string nasmText, int nasmLine)
    {
        var lines = nasmText.Split('\n');
        if (nasmLine < 1 || nasmLine > lines.Length)
            return nasmText;

        var sb = new StringBuilder();
        for (int i = 0; i < lines.Length; i++)
        {
            var prefix = i + 1 == nasmLine ? ">>> " : "    ";
            sb.Append(prefix);
            sb.AppendLine(lines[i].TrimEnd('\r'));
        }
        return sb.ToString();
    }

    public string GetDisasmText(string? nasmText, SourceMapDocument? map, string? binaryPath = null)
        => DisasmService.FormatDisasm(nasmText, map, binaryPath);

    public string ExplainForAgent(string sourcePath, string sourceText, string target)
        => ExplainAgentService.ExplainForAgentJson(sourcePath, sourceText, target);

    public string GetPlanText(string sourcePath, string target)
    {
        if (sourcePath is "(unsaved)" or "")
            return "Save source to a file to view compilation plan.";
        return PlanService.FormatPlanText(sourcePath, target);
    }

    public string GetDiffText(string oldText, string newText)
        => SemanticDiffService.FormatDiffText(oldText, newText);

    public IEnumerable<string> FindHla64Files(string folder)
    {
        if (!Directory.Exists(folder))
            yield break;
        foreach (var file in Directory.EnumerateFiles(folder, "*.hla64", SearchOption.AllDirectories))
            yield return file;
    }

    public bool HasProjectManifest(string folder)
        => File.Exists(Path.Combine(folder, "hla64.toml"));

    private static CompilationOptions BuildOptions(string target, bool emitSourceMap)
        => CompilationOptions.Default with
        {
            Target = TargetTriple.Parse(target),
            EmitSourceMap = emitSourceMap
        };

    private static List<LabDiagnosticItem> ToDiagnosticItems(IEnumerable<Diagnostic> diagnostics)
        => diagnostics.Select(d => new LabDiagnosticItem
        {
            Line = d.Line,
            Code = d.Code,
            Message = d.Message,
            Severity = d.Severity.ToString().ToLowerInvariant()
        }).ToList();

    private static string FormatIr(ExplainReport report)
    {
        var sb = new StringBuilder();
        foreach (var fn in report.IrFunctions)
        {
            sb.AppendLine(fn.ToString());
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private static string FormatAbi(ExplainReport report, CompilationOptions options, string sourceText)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"target: {options.Target}");
        foreach (var fn in report.LoweredFunctions)
        {
            sb.AppendLine(ExplainReport.DescribeLowered(fn));
            sb.AppendLine();
        }

        if (report.Success)
        {
            var verify = ExplainReport.CollectVerificationWarnings(sourceText, options);
            foreach (var w in verify.ClobberWarnings)
                sb.AppendLine($"clobber: {w}");
            foreach (var issue in verify.AbiIssues)
                sb.AppendLine($"ABI: {issue}");
        }

        return sb.ToString().TrimEnd();
    }
}
