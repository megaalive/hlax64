namespace HlaX64.Compiler.Options;

public sealed record CompilationOptions(
    TargetTriple Target,
    OutputKind OutputKind,
    RuntimeMode RuntimeMode,
    OptimizationLevel Optimization,
    bool EmitDebugInfo,
    CompilerWarnings Warnings)
{
    public static CompilationOptions Default { get; } = new(
        Target: TargetTriple.LinuxX64SysV,
        OutputKind: OutputKind.Executable,
        RuntimeMode: RuntimeMode.Inline,
        Optimization: OptimizationLevel.None,
        EmitDebugInfo: false,
        Warnings: new CompilerWarnings());
}
