using HlaX64.Compiler.Cpu;

namespace HlaX64.Compiler.Options;

public sealed record CompilationOptions(
    TargetTriple Target,
    OutputKind OutputKind,
    RuntimeMode RuntimeMode,
    OptimizationLevel Optimization,
    bool EmitDebugInfo,
    bool EmitSourceMap,
    bool TraceProcedures,
    CpuFeatureSet CpuFeatures,
    RegisterAllocationMode RegisterMode,
    CompilerWarnings Warnings)
{
    public static CompilationOptions Default { get; } = new(
        Target: TargetTriple.LinuxX64SysV,
        OutputKind: OutputKind.Executable,
        RuntimeMode: RuntimeMode.Inline,
        Optimization: OptimizationLevel.None,
        EmitDebugInfo: false,
        EmitSourceMap: false,
        TraceProcedures: false,
        CpuFeatures: CpuFeatureSet.BaselineX64,
        RegisterMode: RegisterAllocationMode.Explicit,
        Warnings: new CompilerWarnings());
}
