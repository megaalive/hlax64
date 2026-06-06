namespace HlaX64.Compiler.Options;

public sealed record CompilerWarnings(
    bool Bounds = false,
    bool DefiniteAssignment = false,
    bool Unreachable = false,
    bool Liveness = false)
{
    /// <summary>Default verification warnings for LSP diagnostics.</summary>
    public static CompilerWarnings LanguageServerDefaults { get; } = new(
        Bounds: true,
        DefiniteAssignment: true,
        Unreachable: true,
        Liveness: true);

    public static CompilerWarnings WithVerifyAll(bool enabled) => enabled
        ? new CompilerWarnings(DefiniteAssignment: true, Unreachable: true, Liveness: true)
        : new CompilerWarnings();
}
