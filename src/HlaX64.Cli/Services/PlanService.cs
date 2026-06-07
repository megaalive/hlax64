using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Cli.Services;

public static class PlanService
{
    public static object BuildPlan(string sourcePath, string? target = null)
    {
        var sourceFile = Path.GetFullPath(sourcePath);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var triple = target ?? "linux-x64-sysv";
        var isWindows = triple.Contains("windows", StringComparison.OrdinalIgnoreCase)
            || TargetTriple.Parse(triple).Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase);
        var outputDir = Path.Combine(Path.GetDirectoryName(sourceFile)!, "build", sourceName);
        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objExt = isWindows ? ".obj" : ".o";
        var exeExt = isWindows ? ".exe" : "";
        var objFile = Path.Combine(outputDir, $"{sourceName}{objExt}");
        var exeFile = Path.Combine(outputDir, $"{sourceName}{exeExt}");
        var nasmFormat = isWindows ? "win64" : "elf64";
        var linkCmd = isWindows
            ? $"lld-link \"{objFile}\" /out:\"{exeFile}\""
            : $"ld \"{objFile}\" -o \"{exeFile}\"";

        return new
        {
            target = triple,
            compilerVersion = Compilation.GetVersion(),
            source = sourceFile,
            toolchain = new[]
            {
                new { step = "compile", command = $"hla64 emit-nasm \"{sourceFile}\" --target {triple} -o \"{nasmFile}\"" },
                new { step = "assemble", command = $"nasm -f {nasmFormat} \"{nasmFile}\" -o \"{objFile}\"" },
                new { step = "link", command = linkCmd }
            },
            artifacts = new[] { nasmFile, objFile, exeFile },
            nasmAvailable = NasmTool.TryFindNasm(out _)
        };
    }

    public static string FormatPlanText(string sourcePath, string? target = null)
    {
        var plan = BuildPlan(sourcePath, target);
        var sourceFile = Path.GetFullPath(sourcePath);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var triple = target ?? "linux-x64-sysv";
        var isWindows = triple.Contains("windows", StringComparison.OrdinalIgnoreCase);
        var outputDir = Path.Combine(Path.GetDirectoryName(sourceFile)!, "build", sourceName);
        var nasmFile = Path.Combine(outputDir, $"{sourceName}.nasm");
        var objExt = isWindows ? ".obj" : ".o";
        var exeExt = isWindows ? ".exe" : "";
        var objFile = Path.Combine(outputDir, $"{sourceName}{objExt}");
        var exeFile = Path.Combine(outputDir, $"{sourceName}{exeExt}");

        return $"""
            Compilation plan for {sourceFile}:
              target: {triple}
              emit-nasm -> {nasmFile}
              assemble -> {objFile}
              link -> {exeFile}
              nasm available: {NasmTool.TryFindNasm(out _)}
            """;
    }
}
