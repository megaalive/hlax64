using HlaX64.Cli.Commands;
using HlaX64.Cli.Json;
using HlaX64.Cli.Services;
using HlaX64.Compiler;
using HlaX64.Compiler.Analysis;
using HlaX64.Compiler.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HlaX64.Cli.Services;

public static class ProofBundleWriter
{
    public static string Write(
        string sourceFile,
        string sourceText,
        CompilationOptions options,
        string outputDir,
        CompileArtifacts artifacts,
        string nasmFile,
        string? objFile,
        string outputFile,
        bool includeTestsSummary = false,
        string? projectDir = null,
        bool compileOnly = false)
    {
        var bundleDir = Path.Combine(outputDir, "proof-bundle");
        Directory.CreateDirectory(bundleDir);

        File.WriteAllText(Path.Combine(bundleDir, Path.GetFileName(nasmFile)), artifacts.NasmCode);
        if (File.Exists(outputFile))
            File.Copy(outputFile, Path.Combine(bundleDir, Path.GetFileName(outputFile)), overwrite: true);

        if (artifacts.SourceMap != null)
            File.WriteAllText(Path.Combine(bundleDir, Path.GetFileNameWithoutExtension(sourceFile) + ".hlamap.json"),
                artifacts.SourceMap.ToJson());

        var capabilities = CapabilityAnalyzer.Analyze(sourceText);
        File.WriteAllText(Path.Combine(bundleDir, "capabilities.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            filesystemAccess = capabilities.FilesystemAccess,
            syscalls = capabilities.Syscalls,
            externalLibraries = capabilities.ExternalLibraries,
            externProcedures = capabilities.ExternProcedures,
            hasStdoutPut = capabilities.HasStdoutPut,
            hasExtern = capabilities.HasExtern
        }, new JsonSerializerOptions { WriteIndented = true }));

        var report = ExplainReport.Create(sourceFile, sourceText, options);
        if (report.Success)
        {
            File.WriteAllText(Path.Combine(bundleDir, "ir.json"), JsonSerializer.Serialize(new
            {
                ir = report.IrFunctions.Select(f => f.ToString()).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));

            File.WriteAllText(Path.Combine(bundleDir, "abi.json"), JsonSerializer.Serialize(new
            {
                target = options.Target.ToString(),
                lowered = report.LoweredFunctions.Select(ExplainReport.DescribeLowered).ToArray()
            }, new JsonSerializerOptions { WriteIndented = true }));
        }

        if (includeTestsSummary)
        {
            var testsPath = Path.Combine(projectDir ?? Path.GetDirectoryName(sourceFile)!, "tests.json");
            if (File.Exists(testsPath))
                File.Copy(testsPath, Path.Combine(bundleDir, "tests.json"), overwrite: true);
        }

        var artifactsList = new List<string> { "nasm", "ir.json", "hlamap.json", "abi.json", "capabilities.json", "build.json" };
        if (File.Exists(Path.Combine(bundleDir, Path.GetFileName(outputFile))))
            artifactsList.Insert(0, "binary");
        if (includeTestsSummary && File.Exists(Path.Combine(bundleDir, "tests.json")))
            artifactsList.Add("tests.json");

        var buildMeta = new
        {
            schemaVersion = 1,
            compilerVersion = Compilation.GetVersion(),
            source = Path.GetFullPath(sourceFile),
            sourceSha256 = Sha256Hex(sourceText),
            target = options.Target.ToString(),
            outputKind = options.OutputKind.ToString().ToLowerInvariant(),
            optimization = options.Optimization.ToString(),
            compileOnly,
            artifacts = artifactsList
        };
        File.WriteAllText(Path.Combine(bundleDir, "build.json"),
            JsonSerializer.Serialize(buildMeta, new JsonSerializerOptions { WriteIndented = true }));

        return bundleDir;
    }

    private static string Sha256Hex(string text)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
