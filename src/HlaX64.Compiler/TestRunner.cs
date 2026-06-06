using System.Diagnostics;

namespace HlaX64.Compiler;

/// <summary>
/// Runs HLA-X64 test programs: compile → assemble → link → execute → assert.
/// </summary>
public sealed class TestRunner
{
    private readonly string? _nasmPath;
    private readonly bool _skipExecution;
    private readonly Func<string, string>? _compileFunc;

    /// <summary>
    /// Creates a TestRunner that can compile and optionally execute HLA-X64 programs.
    /// </summary>
    /// <param name="compileFunc">Function that takes HLA source text and returns NASM code.</param>
    /// <param name="nasmPath">Path to NASM binary. If null, assembly steps are skipped.</param>
    /// <param name="skipExecution">If true, only compile (no assemble/link/run).</param>
    public TestRunner(Func<string, string>? compileFunc = null, string? nasmPath = null, bool skipExecution = false)
    {
        _compileFunc = compileFunc;
        _nasmPath = nasmPath;
        _skipExecution = skipExecution;
    }

    /// <summary>
    /// Run a single test manifest.
    /// </summary>
    public TestResult RunTest(TestManifest test, string buildDir)
    {
        var result = new TestResult { Name = test.Name };
        var sw = Stopwatch.StartNew();

        try
        {
            Directory.CreateDirectory(buildDir);

            var sourcePath = ResolveSourcePath(test.Source);
            if (!File.Exists(sourcePath))
            {
                result.Passed = false;
                result.ErrorMessage = $"Source file not found: {sourcePath}";
                return result;
            }

            var sourceText = File.ReadAllText(sourcePath);

            if (_compileFunc == null)
            {
                result.Passed = false;
                result.ErrorMessage = "No compile function provided";
                return result;
            }

            string nasmCode;
            try
            {
                nasmCode = _compileFunc(sourceText);
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.CompileFailed = true;
                result.ErrorMessage = $"Compilation failed: {ex.Message}";
                return result;
            }

            var nasmFile = Path.Combine(buildDir, $"{test.Name}.nasm");
            File.WriteAllText(nasmFile, nasmCode);

            if (_skipExecution)
            {
                result.Passed = true;
                return result;
            }

            if (_nasmPath == null)
            {
                result.Passed = false;
                result.ErrorMessage = "NASM not available for execution tests";
                return result;
            }

            var objFile = Path.Combine(buildDir, $"{test.Name}.o");
            var nasmProcess = Process.Start(new ProcessStartInfo
            {
                FileName = _nasmPath,
                Arguments = $"-f elf64 \"{nasmFile}\" -o \"{objFile}\"",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            nasmProcess?.WaitForExit(10000);

            if (nasmProcess?.ExitCode != 0)
            {
                result.Passed = false;
                result.ErrorMessage = $"NASM assembly failed: {nasmProcess?.StandardError.ReadToEnd()}";
                return result;
            }

            var exeFile = Path.Combine(buildDir, $"{test.Name}.exe");
            var linkProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "gcc",
                Arguments = $"\"{objFile}\" -o \"{exeFile}\" -no-pie",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            linkProcess?.WaitForExit(10000);

            if (linkProcess?.ExitCode != 0)
            {
                result.Passed = false;
                result.ErrorMessage = $"Link failed: {linkProcess?.StandardError.ReadToEnd()}";
                return result;
            }

            var runProcess = Process.Start(new ProcessStartInfo
            {
                FileName = exeFile,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });

            runProcess?.WaitForExit(test.TimeoutMs);

            if (runProcess == null)
            {
                result.Passed = false;
                result.ErrorMessage = "Failed to start executable";
                return result;
            }

            result.ActualStdout = runProcess.StandardOutput.ReadToEnd();
            result.ActualExitCode = runProcess.ExitCode;

            if (result.ActualExitCode != test.ExpectedExitCode)
            {
                result.Passed = false;
                result.ErrorMessage = $"Exit code mismatch: expected {test.ExpectedExitCode}, got {result.ActualExitCode}";
                return result;
            }

            if (test.ExpectedStdout != null)
            {
                var expected = test.ExpectedStdout.Replace("\\n", "\n").Replace("\\t", "\t").Replace("\\r", "\r");
                if (result.ActualStdout != expected)
                {
                    result.Passed = false;
                    result.ErrorMessage = $"stdout mismatch: expected \"{expected}\", got \"{result.ActualStdout}\"";
                    return result;
                }
            }

            result.Passed = true;
        }
        catch (Exception ex)
        {
            result.Passed = false;
            result.ErrorMessage = $"Exception: {ex.Message}";
        }
        finally
        {
            sw.Stop();
            result.Duration = sw.Elapsed;
        }

        return result;
    }

    /// <summary>
    /// Run all test manifests from a directory.
    /// </summary>
    public List<TestResult> RunAll(string testsDirectory, string buildBaseDir)
    {
        var manifests = TestManifest.LoadAll(testsDirectory);
        var results = new List<TestResult>();

        foreach (var manifest in manifests)
        {
            var buildDir = Path.Combine(buildBaseDir, manifest.Name);
            results.Add(RunTest(manifest, buildDir));
        }

        return results;
    }

    /// <summary>
    /// Resolves a source path for the test. If the path is absolute it is
    /// used as-is. If relative, the runner searches in this order:
    ///   1. Relative to the current working directory.
    ///   2. Relative to the runner's base directory
    ///      (<c>AppContext.BaseDirectory</c>, where csproj-copied test
    ///      inputs land under <c>examples/</c> and <c>tests/samples/</c>).
    ///   3. Walking up parent directories until a match is found or the
    ///      filesystem root is reached (covers repo-root layouts).
    /// The first match wins.
    /// </summary>
    private static string ResolveSourcePath(string source)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        if (Path.IsPathRooted(source) && File.Exists(source))
            return source;

        // 1. CWD
        var cwdCandidate = Path.GetFullPath(source);
        if (File.Exists(cwdCandidate))
            return cwdCandidate;

        // 2. Base directory (where test runtime copies examples/, etc.)
        var baseCandidate = Path.Combine(AppContext.BaseDirectory, source);
        if (File.Exists(baseCandidate))
            return Path.GetFullPath(baseCandidate);

        // 3. Walk up looking for the file
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, source);
            if (File.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }

        // Fall back to the CWD candidate so the error message is helpful.
        return cwdCandidate;
    }
}
