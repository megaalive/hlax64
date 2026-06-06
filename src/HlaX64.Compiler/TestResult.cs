namespace HlaX64.Compiler;

/// <summary>
/// Result of running a single test.
/// </summary>
public sealed class TestResult
{
    public string Name { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ActualStdout { get; set; }
    public int ActualExitCode { get; set; }
    public TimeSpan Duration { get; set; }
    public bool CompileFailed { get; set; }

    public override string ToString()
    {
        if (Passed)
            return $"PASS: {Name} ({Duration.TotalMilliseconds:F0}ms)";
        if (CompileFailed)
            return $"FAIL (compile): {Name} - {ErrorMessage}";
        return $"FAIL: {Name} - {ErrorMessage}\n  Expected exit: 0, got: {ActualExitCode}\n  Actual stdout: \"{ActualStdout}\"";
    }
}