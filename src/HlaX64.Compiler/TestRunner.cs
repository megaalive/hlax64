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
    private readonly CompileWithResult? _compileWithResultFunc;
    private readonly LinkExtrasBuilder? _linkExtrasBuilder;
    private readonly LinkerRunner? _linkerRunner;
    private readonly BinaryExecutor? _binaryExecutor;

    /// <summary>
    /// Delegate that takes a NASM file and an object file, returns the
    /// assembled object file path and an error message. Used to abstract
    /// away the choice between native nasm and WSL nasm.
    /// </summary>
    public delegate (bool Ok, string Error) Assembler(string nasmFile, string objFile);

    /// <summary>
    /// Delegate that takes an object file and an output executable path,
    /// returns whether linking succeeded and an error message. The
    /// implementation is responsible for choosing the right toolchain
    /// (native gcc, WSL gcc, MinGW, etc.).
    /// </summary>
    public delegate (bool Ok, string Error, bool RequiresWsl) LinkerRunner(
        string objFile,
        string exeFile,
        IReadOnlyList<string>? extraLibraries = null);

    /// <summary>
    /// Builds runtime/cache link extras (e.g. hlax64-runtime-file *.o and -lc).
    /// </summary>
    public delegate (bool Ok, IReadOnlyList<string> Extras, string? Error) LinkExtrasBuilder(
        CompilationResult result,
        string buildDir,
        string sourcePath);

    /// <summary>
    /// Compiles source to NASM plus a <see cref="CompilationResult"/> for runtime linking.
    /// </summary>
    public delegate (string Nasm, CompilationResult Result) CompileWithResult(string sourcePath, string sourceText);

    /// <summary>
    /// Delegate that runs an executable and returns its exit code, stdout,
    /// stderr, and a flag indicating whether WSL is required to invoke it.
    /// </summary>
    public delegate (int ExitCode, string Stdout, string Stderr) BinaryExecutor(string exeFile, int timeoutMs);

    /// <summary>
    /// Creates a TestRunner that can compile and optionally execute HLA-X64 programs.
    /// </summary>
    /// <param name="compileFunc">Function that takes HLA source text and returns NASM code.</param>
    /// <param name="compileWithResultFunc">Like compileFunc but also returns the compilation result.</param>
    /// <param name="linkExtrasBuilder">Optional runtime link extras builder (requires compileWithResultFunc).</param>
    /// <param name="nasmPath">Path to NASM binary. If null, assembly steps are skipped.</param>
    /// <param name="skipExecution">If true, only compile (no assemble/link/run).</param>
    /// <param name="linkerRunner">Delegate that invokes the linker (e.g. WSL gcc).</param>
    /// <param name="binaryExecutor">Delegate that runs the linked executable (e.g. WSL exec).</param>
    public TestRunner(
        Func<string, string>? compileFunc = null,
        CompileWithResult? compileWithResultFunc = null,
        LinkExtrasBuilder? linkExtrasBuilder = null,
        string? nasmPath = null,
        bool skipExecution = false,
        LinkerRunner? linkerRunner = null,
        BinaryExecutor? binaryExecutor = null)
    {
        _compileFunc = compileFunc;
        _compileWithResultFunc = compileWithResultFunc;
        _linkExtrasBuilder = linkExtrasBuilder;
        _nasmPath = nasmPath;
        _skipExecution = skipExecution;
        _linkerRunner = linkerRunner;
        _binaryExecutor = binaryExecutor;
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

            var sourcePath = ResolveSourcePath(test.Source, test.ManifestPath);
            if (!File.Exists(sourcePath))
            {
                result.Passed = false;
                result.ErrorMessage = $"Source file not found: {sourcePath}";
                return result;
            }

            var sourceText = File.ReadAllText(sourcePath);

            if (_compileFunc == null && _compileWithResultFunc == null)
            {
                result.Passed = false;
                result.ErrorMessage = "No compile function provided";
                return result;
            }

            string nasmCode;
            CompilationResult? compilationResult = null;
            try
            {
                if (_compileWithResultFunc != null)
                {
                    (nasmCode, compilationResult) = _compileWithResultFunc(sourcePath, sourceText);
                }
                else
                {
                    nasmCode = _compileFunc!(sourceText);
                }
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

            // Assemble (nasm may be native or `wsl nasm <args>`)
            // The path may be encoded as "fileName|prefixArgs" by NasmTool
            // (e.g. "wsl|nasm") to preserve the WSL wrapper invocation.
            string nasmFileName = _nasmPath!;
            string nasmPrefixArgs = string.Empty;
            var pipeIdx = _nasmPath!.IndexOf('|');
            if (pipeIdx >= 0)
            {
                nasmFileName = _nasmPath[..pipeIdx];
                nasmPrefixArgs = _nasmPath[(pipeIdx + 1)..];
            }
            var nasmArgs = string.IsNullOrEmpty(nasmPrefixArgs)
                ? $"-f elf64 \"{nasmFile}\" -o \"{objFile}\""
                : $"{nasmPrefixArgs} -f elf64 \"{nasmFile}\" -o \"{objFile}\"";
            var nasmProcess = Process.Start(new ProcessStartInfo
            {
                FileName = nasmFileName,
                Arguments = nasmArgs,
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

            var exeFile = Path.Combine(buildDir, test.Name);

            // Link via injected delegate (CLI supplies WSL-aware logic)
            if (_linkerRunner == null)
            {
                result.Passed = false;
                result.ErrorMessage = "No linker runner provided; cannot link object file";
                return result;
            }
            IReadOnlyList<string>? linkExtras = null;
            if (_linkExtrasBuilder != null)
            {
                if (compilationResult == null)
                {
                    result.Passed = false;
                    result.ErrorMessage = "Link extras builder requires compileWithResultFunc";
                    return result;
                }

                var (extrasOk, extras, extrasErr) = _linkExtrasBuilder(compilationResult, buildDir, sourcePath);
                if (!extrasOk)
                {
                    result.Passed = false;
                    result.ErrorMessage = extrasErr ?? "Failed to build runtime link extras";
                    return result;
                }

                linkExtras = extras;
            }

            var (linkOk, linkErr, _) = _linkerRunner(objFile, exeFile, linkExtras);
            if (!linkOk)
            {
                result.Passed = false;
                result.ErrorMessage = $"Link failed: {linkErr}";
                return result;
            }

            // Run via injected delegate (CLI supplies WSL exec logic)
            if (_binaryExecutor == null)
            {
                result.Passed = false;
                result.ErrorMessage = "No binary executor provided; cannot run executable";
                return result;
            }
            (result.ActualExitCode, result.ActualStdout, _) =
                _binaryExecutor(exeFile, test.TimeoutMs);

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
    ///   0. Relative to the directory containing the manifest file
    ///      (<paramref name="manifestPath"/>) — this is the convention
    ///      that makes "source": "hello.hla64" inside
    ///      <c>tests/samples/hello/manifest.json</c> find the sibling
    ///      source file.
    ///   1. Relative to the current working directory.
    ///   2. Relative to the runner's base directory
    ///      (<c>AppContext.BaseDirectory</c>, where csproj-copied test
    ///      inputs land under <c>examples/</c> and <c>tests/samples/</c>).
    ///   3. Walking up parent directories until a match is found or the
    ///      filesystem root is reached (covers repo-root layouts).
    /// The first match wins.
    /// </summary>
    private static string ResolveSourcePath(string source, string? manifestPath)
    {
        if (string.IsNullOrEmpty(source))
            return source;

        if (Path.IsPathRooted(source) && File.Exists(source))
            return source;

        // 0. Manifest's own directory
        if (!string.IsNullOrEmpty(manifestPath))
        {
            var manifestDir = Path.GetDirectoryName(manifestPath);
            if (!string.IsNullOrEmpty(manifestDir))
            {
                var manifestCandidate = Path.Combine(manifestDir, source);
                if (File.Exists(manifestCandidate))
                    return Path.GetFullPath(manifestCandidate);
            }
        }

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
