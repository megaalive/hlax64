using System.Collections.Frozen;

namespace HlaX64.Cli.Toolchain;

/// <summary>Assembles and caches HlaX64.Runtime NASM objects for linking.</summary>
public static class RuntimeObjectProvider
{
    private static readonly FrozenSet<string> StdoutSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stdout_put_str", "stdout_put_nl", "stdout_put_int", "stdout_put_uint"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ConversionSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "stdout_put_int", "stdout_put_uint", "int_to_str", "uint_to_str"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> ArgvSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_argv_init", "hlax_argv_count", "hlax_argv_get"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> HeapSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_malloc", "hlax_realloc", "hlax_free"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> FileSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_path_exists", "hlax_file_open_read", "hlax_file_open_write",
        "hlax_file_read", "hlax_file_write", "hlax_file_close", "hlax_stdout_write"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> MemSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_strlen", "hlax_memcpy", "hlax_memset", "hlax_is_space", "hlax_is_printable"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> SysSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_getpid", "hlax_hostname", "hlax_uptime_secs",
        "hlax_mem_total", "hlax_mem_avail", "hlax_file_size",
        "hlax_os_last_error", "hlax_cpu_count", "hlax_disk_total_bytes",
        "hlax_disk_avail_bytes", "hlax_self_rss_bytes", "hlax_load_avg_milli"
    }.ToFrozenSet();

    private static readonly FrozenSet<string> NetSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "hlax_net_init", "hlax_net_last_error", "hlax_dns_resolve_v4",
        "hlax_tcp_connect", "hlax_tcp_connect_name", "hlax_tcp_connect_timeout",
        "hlax_tcp_set_timeouts_ms", "hlax_tcp_write", "hlax_tcp_write_all",
        "hlax_tcp_read", "hlax_tcp_read_once", "hlax_tcp_close"
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
        bool needHeap = externs.Any(HeapSymbols.Contains);
        bool needFile = externs.Any(FileSymbols.Contains);
        bool needMem = externs.Any(MemSymbols.Contains);
        bool needSys = externs.Any(SysSymbols.Contains);
        bool needNet = externs.Any(NetSymbols.Contains);
        if (needStdout)
            needConversion = true;
        if (!needStdout && !needConversion && !needArgv && !needHeap && !needFile && !needMem && !needSys && !needNet)
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

        if (needHeap)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "heap.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-heap", "elf64", out var heapObj, out error))
                return false;
            objects.Add(heapObj);
        }

        if (needFile)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "file.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-file", "elf64", out var fileObj, out error))
                return false;
            objects.Add(fileObj);
        }

        if (needMem)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "mem.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-mem", "elf64", out var memObj, out error))
                return false;
            objects.Add(memObj);
        }

        if (needSys)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "sys.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-sys", "elf64", out var sysObj, out error))
                return false;
            objects.Add(sysObj);
        }

        if (needNet)
        {
            var nasm = Path.Combine(runtimeDir, "linux-x64", "net.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-net", "elf64", out var netObj, out error))
                return false;
            objects.Add(netObj);
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
        bool needHeap = externs.Any(HeapSymbols.Contains);
        bool needFile = externs.Any(FileSymbols.Contains);
        bool needMem = externs.Any(MemSymbols.Contains);
        bool needSys = externs.Any(SysSymbols.Contains);
        bool needNet = externs.Any(NetSymbols.Contains);
        // stdout.nasm references int_to_str for stdout_put_int — assemble conversion too.
        if (needStdout)
            needConversion = true;
        if (!needStdout && !needConversion && !needArgv && !needHeap && !needFile && !needMem && !needSys && !needNet)
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

        if (needHeap)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "heap.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-heap", "win64", out var heapObj, out error))
                return false;
            objects.Add(heapObj);
        }

        if (needFile)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "file.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-file", "win64", out var fileObj, out error))
                return false;
            objects.Add(fileObj);
        }

        if (needMem)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "mem.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-mem", "win64", out var memObj, out error))
                return false;
            objects.Add(memObj);
        }

        if (needSys)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "sys.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-sys", "win64", out var sysObj, out error))
                return false;
            objects.Add(sysObj);
        }

        if (needNet)
        {
            var nasm = Path.Combine(runtimeDir, "windows-x64", "net.nasm");
            if (!TryAssembleRuntime(nasm, cacheDirectory, "hlax64-runtime-net", "win64", out var netObj, out error))
                return false;
            objects.Add(netObj);
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
        bool needHeap = externs.Any(HeapSymbols.Contains);
        bool needFile = externs.Any(FileSymbols.Contains);
        bool needSys = externs.Any(SysSymbols.Contains);
        bool needNet = externs.Any(NetSymbols.Contains);
        if (isWindows)
        {
            if (!TryGetWindowsRuntimeObjects(externs, cacheDirectory, out var runtimeObjs, out error))
                return false;
            linkExtras.AddRange(runtimeObjs);
            if (needNet && !linkExtras.Contains("ws2_32.lib", StringComparer.OrdinalIgnoreCase))
                linkExtras.Add("ws2_32.lib");
            if (needSys && !linkExtras.Contains("psapi.lib", StringComparer.OrdinalIgnoreCase))
                linkExtras.Add("psapi.lib");
            if (isSharedLibrary)
            {
                if (!TryAssembleRuntimeDllMain(cacheDirectory, out var dllMainObj, out error) ||
                    string.IsNullOrEmpty(dllMainObj))
                    return false;
                linkExtras.Add(dllMainObj);
            }
        }
        else
        {
            if (!TryGetLinuxRuntimeObjects(externs, cacheDirectory, out var runtimeObjs, out error))
                return false;
            linkExtras.AddRange(runtimeObjs);
            if ((needHeap || needFile || needSys || needNet) && !linkExtras.Contains("-lc", StringComparer.OrdinalIgnoreCase))
                linkExtras.Add("-lc");
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

        var resolved = ToolchainResolver.Default.ResolveRuntimeDirectory();
        if (resolved.Found && !string.IsNullOrWhiteSpace(resolved.Path))
            return resolved.Path;

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
