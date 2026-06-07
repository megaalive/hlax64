using System.Collections.Frozen;

namespace HlaX64.Cli.Toolchain;

/// <summary>Assembles and caches HlaX64.Runtime NASM objects for linking.</summary>
public static class RuntimeObjectProvider
{
    private static readonly FrozenSet<string> StdoutSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stdout_put_str", "stdout_put_nl", "stdout_put_int"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ConversionSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stdout_put_int", "int_to_str"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ArgvSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_argv_init", "hlax_argv_count", "hlax_argv_get"
    }.ToFrozenSet();

    public static bool TryGetLinuxRuntimeObjects(
        IEnumerable<string> requiredExterns,
        string cacheDirectory,
        out IReadOnlyList<string> objectFiles,
        out string? error)
    {
        objectFiles = [];
        error = null;

        var externs = requiredExterns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool needStdout = externs.Any(StdoutSymbols.Contains);
        bool needConversion = externs.Any(ConversionSymbols.Contains);
        bool needArgv = externs.Any(ArgvSymbols.Contains);
        if (needStdout)
            needConversion = true;
        if (!needStdout && !needConversion && !needArgv)
            return true;

        var runtimeDir = FindRuntimeDirectory();
        if (runtimeDir == null)
        {
            error = "HlaX64.Runtime sources not found. Clone/build the repo or set HLAX64_RUNTIME_DIR.";
            return false;
        }

        Directory.CreateDirectory(cacheDirectory);
        var objects = new List<string>();

        if (needConversion)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "conversion.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-conversion", "elf64", out var convObj, out error))
                return false;
            objects.Add(convObj);
        }

        if (needStdout)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "stdout.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-stdout", "elf64", out var stdoutObj, out error))
                return false;
            objects.Add(stdoutObj);
        }

        if (needArgv)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "argv.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-argv", "elf64", out var argvObj, out error))
                return false;
            objects.Add(argvObj);
        }

        objectFiles = objects;
        return true;
    }

    public static bool TryGetWindowsRuntimeObjects(
        IEnumerable<string> requiredExterns,
        string cacheDirectory,
        out IReadOnlyList<string> objectFiles,
        out string? error)
    {
        objectFiles = [];
        error = null;

        var externs = requiredExterns.ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool needStdout = externs.Any(StdoutSymbols.Contains);
        bool needConversion = externs.Any(ConversionSymbols.Contains);
        bool needArgv = externs.Any(ArgvSymbols.Contains);
        // stdout.nasm references int_to_str for stdout_put_int — assemble conversion too.
        if (needStdout)
            needConversion = true;
        if (!needStdout && !needConversion && !needArgv)
            return true;

        var runtimeDir = FindRuntimeDirectory();
        if (runtimeDir == null)
        {
            error = "HlaX64.Runtime sources not found. Clone/build the repo or set HLAX64_RUNTIME_DIR.";
            return false;
        }

        Directory.CreateDirectory(cacheDirectory);
        var objects = new List<string>();

        if (needConversion)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "conversion.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-conversion", "win64", out var convObj, out error))
                return false;
            objects.Add(convObj);
        }

        if (needStdout)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "stdout.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-stdout", "win64", out var stdoutObj, out error))
                return false;
            objects.Add(stdoutObj);
        }

        if (needArgv)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "argv.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-argv", "win64", out var argvObj, out error))
                return false;
            objects.Add(argvObj);
        }

        objectFiles = objects;
        return true;
    }

    public static IEnumerable<string> CollectRequiredExterns(
        IEnumerable<HlaX64.Compiler.Abi.LoweredFunction> loweredFunctions)
        => loweredFunctions.SelectMany(f => f.RequiredExterns).Distinct(StringComparer.OrdinalIgnoreCase);

    public static bool TryBuildLinkExtras(
        HlaX64.Compiler.CompilationResult result,
        bool isWindows,
        string cacheDirectory,
        out List<string> linkExtras,
        out string? error,
        bool isSharedLibrary = false)
    {
        linkExtras = result.LinkLibraries.ToList();
        var externs = CollectRequiredExterns(result.LoweredFunctions);
        if (isWindows)
        {
            if (!TryGetWindowsRuntimeObjects(externs, cacheDirectory, out var runtimeObjs, out error))
                return false;
            linkExtras.AddRange(runtimeObjs);
            if (isSharedLibrary)
            {
                if (!TryAssembleRuntimeDllMain(cacheDirectory, out var dllMainObj, out error))
                    return false;
                linkExtras.Add(dllMainObj);
            }
        }
        else
        {
            if (!TryGetLinuxRuntimeObjects(externs, cacheDirectory, out var runtimeObjs, out error))
                return false;
            linkExtras.AddRange(runtimeObjs);
        }

        error = null;
        return true;
    }

    private static bool TryAssembleRuntimeDllMain(string cacheDirectory, out string? objectFile, out string? error)
    {
        objectFile = null;
        var runtimeDir = FindRuntimeDirectory();
        if (runtimeDir == null)
        {
            error = "HlaX64.Runtime sources not found. Clone/build the repo or set HLAX64_RUNTIME_DIR.";
            return false;
        }

        var nasm = Path.Combine(runtimeDir, "windows-x64", "dllmain.nasm");
        return TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-dllmain", "win64", out objectFile, out error);
    }

    private static bool TryAssembleRuntime(
        string nasmSource,
        string cacheDirectory,
        string baseName,
        string format,
        out string objectFile,
        out string? error)
    {
        objectFile = "";
        error = null;

        if (!File.Exists(nasmSource))
        {
            error = $"Runtime NASM not found: {nasmSource}";
            return false;
        }

        if (!NasmTool.TryFindNasm(out var nasmPath))
        {
            error = "NASM not found. Install NASM (https://nasm.us)";
            return false;
        }

        var sourceHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(nasmSource)))[..12];
        var nasmFile = Path.Combine(cacheDirectory, $"{baseName}-{sourceHash}.nasm");
        var objFile = Path.Combine(cacheDirectory, $"{baseName}-{sourceHash}.obj");

        if (!File.Exists(objFile) || File.GetLastWriteTimeUtc(nasmSource) > File.GetLastWriteTimeUtc(objFile))
        {
            File.Copy(nasmSource, nasmFile, overwrite: true);
            var nasmTool = new NasmTool(nasmPath);
            if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: format))
            {
                error = $"Failed to assemble runtime {Path.GetFileName(nasmSource)}: {nasmError}";
                return false;
            }
        }

        objectFile = objFile;
        return true;
    }

    public static string? FindRuntimeDirectory()
    {
        var env = Environment.GetEnvironmentVariable("HLAX64_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        foreach (var start in EnumerateSearchRoots())
        {
            var candidate = Path.Combine(start, "src", "HlaX64.Runtime");
            if (Directory.Exists(candidate) &&
                File.Exists(Path.Combine(candidate, "linux-x64", "stdout.nasm")))
                return candidate;
        }

        return null;
    }

    private static IEnumerable<string> EnumerateSearchRoots()
    {
        yield return Directory.GetCurrentDirectory();

        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            yield return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
    }
}
