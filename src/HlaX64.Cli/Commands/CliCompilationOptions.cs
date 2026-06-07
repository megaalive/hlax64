using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using HlaX64.Compiler.Cpu;
using HlaX64.Compiler.Debug;
using HlaX64.Compiler.Options;

namespace HlaX64.Cli.Commands;

public sealed record CompileArtifacts(
    string NasmCode,
    CompilationResult Result,
    SourceMapDocument? SourceMap);

internal static class CliCompilationOptions
{
    internal static CompilationOptions FromCli(
        string? target,
        string? runtimeMode,
        bool warnBounds,
        bool warnDefinite = false,
        bool warnUnreachable = false,
        bool warnLiveness = false,
        bool warnVerify = false,
        string? optimize = null,
        bool emitSourceMap = false,
        bool emitDebugInfo = false,
        bool traceProcedures = false,
        string? cpu = null,
        IEnumerable<string>? features = null,
        string? registerMode = null)
    {
        var options = CompilationOptions.Default with
        {
            Target = TargetTriple.Parse(target ?? "linux-x64-sysv"),
            Optimization = OptimizationLevelParser.Parse(optimize),
            EmitSourceMap = emitSourceMap,
            EmitDebugInfo = emitDebugInfo,
            TraceProcedures = traceProcedures,
            CpuFeatures = CpuFeatureSet.Parse(cpu, features),
            RegisterMode = registerMode?.Equals("assisted", StringComparison.OrdinalIgnoreCase) == true
                ? RegisterAllocationMode.Assisted
                : RegisterAllocationMode.Explicit
        };

        if (runtimeMode?.Equals("library", StringComparison.OrdinalIgnoreCase) == true)
            options = options with { RuntimeMode = HlaX64.Compiler.Options.RuntimeMode.Library };

        var warnings = options.Warnings;
        if (warnBounds)
            warnings = warnings with { Bounds = true };
        if (warnVerify)
            warnings = warnings with { DefiniteAssignment = true, Unreachable = true, Liveness = true };
        else
        {
            if (warnDefinite)
                warnings = warnings with { DefiniteAssignment = true };
            if (warnUnreachable)
                warnings = warnings with { Unreachable = true };
            if (warnLiveness)
                warnings = warnings with { Liveness = true };
        }

        return options with { Warnings = warnings };
    }
}

public static class CompilePipeline
{
    public static string EmitNasm(string sourcePath, string sourceText, CompilationOptions? options = null)
        => Compile(sourcePath, sourceText, options).NasmCode;

    public static CompileArtifacts Compile(string sourcePath, string sourceText, CompilationOptions? options = null)
    {
        var compilation = new Compilation(sourcePath, sourceText, options);
        var result = compilation.Process();

        if (!result.Success)
            throw new InvalidOperationException(string.Join("\n", result.Diagnostics));

        var opts = options ?? CompilationOptions.Default;
        var emitter = new NasmEmitter();
        var nasmCode = emitter.Emit(
            result.LoweredFunctions,
            result.StringLiterals,
            result.GlobalData,
            new NasmEmitOptions
            {
                EmitDebugInfo = opts.EmitDebugInfo,
                TraceProcedures = opts.TraceProcedures,
                AnnotateIrIds = opts.EmitSourceMap,
                SourceFileName = Path.GetFileName(sourcePath),
                IsWindowsTarget = opts.Target.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase),
                IsSharedLibrary = opts.OutputKind == OutputKind.SharedLibrary
            });

        SourceMapDocument? map = null;
        if (opts.EmitSourceMap)
        {
            map = SourceMapBuilder.Build(
                Path.GetFullPath(sourcePath),
                result.IrFunctions,
                result.LoweredFunctions,
                nasmCode,
                Compilation.GetVersion());
            result.SourceMap = map;
        }

        return new CompileArtifacts(nasmCode, result, map);
    }

    public static CompilationResult Process(string sourcePath, string sourceText, CompilationOptions? options = null)
    {
        var compilation = new Compilation(sourcePath, sourceText, options);
        return compilation.Process();
    }
}
