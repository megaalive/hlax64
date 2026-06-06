using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;
using HlaX64.Backend.Nasm.Emitters;
using ModelContextProtocol.Server;

namespace HlaX64.McpServer.Tools;

[McpServerToolType]
public class HlaX64Tools
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    [McpServerTool, Description("Compile a .hla64 source file to NASM assembly")]
    public static string Compile(
        string source,
        string? target = null)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file '{source}' not found.");

        var sourceText = File.ReadAllText(source);
        var triple = TargetTriple.Parse(target ?? "linux-x64-sysv");
        var options = CompilationOptions.Default with { Target = triple };

        var result = CompilePipeline.Process(source, sourceText, options);
        if (!result.Success)
            throw new InvalidOperationException(string.Join("\n", result.Diagnostics));

        var emitter = new NasmEmitter();
        return emitter.Emit(result.LoweredFunctions, result.StringLiterals);
    }

    [McpServerTool, Description("Build a .hla64 source file into an executable")]
    public static string Build(
        string source,
        string? outputDir = null,
        string? target = null,
        string? runtime = null,
        string? outputKind = null)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file '{source}' not found.");

        var sourceFile = Path.GetFullPath(source);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var outDir = outputDir ?? Path.Combine(
            Path.GetDirectoryName(sourceFile)!, "..", "build", sourceName);
        outDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outDir);

        var triple = TargetTriple.Parse(target ?? "linux-x64-sysv");
        var opts = CompilationOptions.Default with { Target = triple };
        if (runtime?.ToLowerInvariant() == "library")
            opts = opts with { RuntimeMode = HlaX64.Compiler.Options.RuntimeMode.Library };

        bool isWindows = triple.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase);
        bool isShared = outputKind?.ToLowerInvariant() == "shared-library";
        string nasmFormat = isWindows ? "win64" : "elf64";
        string objExt = isWindows ? ".obj" : ".o";
        string ext = isShared ? (isWindows ? ".dll" : ".so") : (isWindows ? ".exe" : "");
        string libPrefix = isShared ? "lib" : "";

        var nasmFile = Path.Combine(outDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outDir, $"{sourceName}{objExt}");
        var outputFile = Path.Combine(outDir, $"{libPrefix}{sourceName}{ext}");

        var sourceText = File.ReadAllText(sourceFile);
        var nasmCode = CompilePipeline.EmitNasm(sourceFile, sourceText, opts);
        File.WriteAllText(nasmFile, nasmCode);

        if (!NasmTool.TryFindNasm(out var nasmPath))
            throw new InvalidOperationException("NASM not found. Install NASM (https://nasm.us)");

        var nasmTool = new NasmTool(nasmPath);
        if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: nasmFormat))
            throw new InvalidOperationException($"Assembly error:\n{nasmError}");

        bool linkSuccess;
        string linkError;
        if (isWindows)
            linkSuccess = LinkerTool.TryLinkWindows(objFile, outputFile, out linkError, shared: isShared);
        else
            linkSuccess = LinkerTool.TryLink(objFile, outputFile, out linkError, out _, shared: isShared);

        if (!linkSuccess)
            throw new InvalidOperationException($"Link error:\n{linkError}");

        return $"Build successful: {outputFile}";
    }

    [McpServerTool, Description("Build and run a .hla64 source file, returning stdout and exit code")]
    public static string Run(
        string source,
        string? target = null)
    {
        var sourceFile = Path.GetFullPath(source);
        var sourceName = Path.GetFileNameWithoutExtension(sourceFile);
        var outDir = Path.Combine(
            Path.GetDirectoryName(sourceFile)!, "..", "build", sourceName);
        outDir = Path.GetFullPath(outDir);
        Directory.CreateDirectory(outDir);

        var triple = TargetTriple.Parse(target ?? "linux-x64-sysv");
        var opts = CompilationOptions.Default with { Target = triple };
        bool isWindows = triple.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase);
        string nasmFormat = isWindows ? "win64" : "elf64";
        string objExt = isWindows ? ".obj" : ".o";
        string ext = isWindows ? ".exe" : "";

        var nasmFile = Path.Combine(outDir, $"{sourceName}.nasm");
        var objFile = Path.Combine(outDir, $"{sourceName}{objExt}");
        var exeFile = Path.Combine(outDir, $"{sourceName}{ext}");

        var sourceText = File.ReadAllText(sourceFile);
        var nasmCode = CompilePipeline.EmitNasm(sourceFile, sourceText, opts);
        File.WriteAllText(nasmFile, nasmCode);

        if (!NasmTool.TryFindNasm(out var nasmPath))
            throw new InvalidOperationException("NASM not found.");

        var nasmTool = new NasmTool(nasmPath);
        if (!nasmTool.TryAssemble(nasmFile, objFile, out var nasmError, format: nasmFormat))
            throw new InvalidOperationException($"Assembly error:\n{nasmError}");

        bool requiresWsl;
        if (!LinkerTool.TryLink(objFile, exeFile, out var linkError, out requiresWsl))
            throw new InvalidOperationException($"Link error:\n{linkError}");

        if (!isWindows && !requiresWsl)
        {
            try { Process.Start("chmod", $"+x \"{exeFile}\"")?.WaitForExit(2000); } catch { }
        }

        string fileName = requiresWsl ? "wsl" : exeFile;
        string args = requiresWsl ? LinkerTool.ToWslPath(exeFile) : "";

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        });

        if (process == null)
            throw new InvalidOperationException("Failed to start process.");

        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit(30000);
        if (!process.HasExited)
        {
            process.Kill();
            throw new TimeoutException("Process timed out after 30s.");
        }

        var result = new { exit_code = process.ExitCode, stdout, stderr };
        return JsonSerializer.Serialize(result, JsonOpts);
    }

    [McpServerTool, Description("Run test manifests from a directory, returning pass/fail results")]
    public static string Test(
        string? directory = null,
        bool? compileOnly = null)
    {
        var dir = Path.GetFullPath(directory ?? "tests/samples");
        if (!Directory.Exists(dir))
            throw new DirectoryNotFoundException($"Test directory '{dir}' not found.");

        var buildBase = Path.Combine(Path.GetTempPath(), "hlax64_mcp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(buildBase);

        string? nasmPath = null;
        TestRunner.LinkerRunner? linkerRunner = null;
        TestRunner.BinaryExecutor? binaryExecutor = null;

        if (compileOnly != true)
        {
            NasmTool.TryFindNasm(out nasmPath);

            if (LinkerTool.TryFindLinker(out var linkerPath, out _, out _))
            {
                bool requiresWsl = linkerPath == "wsl";
                linkerRunner = (objF, exeF) =>
                {
                    if (LinkerTool.TryLink(objF, exeF, out var err, out var wsl))
                        return (true, "", wsl);
                    return (false, err, wsl);
                };
                binaryExecutor = (exeF, timeoutMs) =>
                {
                    try
                    {
                        string fn = requiresWsl ? "wsl" : exeF;
                        string a = requiresWsl ? LinkerTool.ToWslPath(exeF) : "";
                        using var p = Process.Start(new ProcessStartInfo
                        {
                            FileName = fn, Arguments = a,
                            RedirectStandardOutput = true, RedirectStandardError = true,
                            UseShellExecute = false, CreateNoWindow = true
                        });
                        if (p == null) return (-1, "", "Failed to start process");
                        if (!p.WaitForExit(timeoutMs)) { p.Kill(); return (-1, "", "Timed out"); }
                        return (p.ExitCode, p.StandardOutput.ReadToEnd(), "");
                    }
                    catch (Exception ex) { return (-1, "", ex.Message); }
                };
            }
        }

        var manifests = TestManifest.LoadAll(dir);
        var results = new List<object>();
        var runner = new TestRunner(
            compileFunc: src => CompilePipeline.EmitNasm("(test)", src),
            nasmPath: nasmPath,
            skipExecution: compileOnly == true,
            linkerRunner: linkerRunner,
            binaryExecutor: binaryExecutor);

        foreach (var manifest in manifests)
        {
            var buildDir = Path.Combine(buildBase, manifest.Name);
            var result = runner.RunTest(manifest, buildDir);
            results.Add(new
            {
                name = result.Name,
                passed = result.Passed,
                duration_ms = result.Duration.TotalMilliseconds,
                error = result.ErrorMessage,
                stdout = result.ActualStdout
            });
        }

        return JsonSerializer.Serialize(new { total = manifests.Count, results }, JsonOpts);
    }

    [McpServerTool, Description("Print ABI details for a target triple")]
    public static string ExplainAbi(string? target = null)
    {
        var triple = (target ?? "linux-x64-sysv").ToLowerInvariant();

        var info = triple switch
        {
            "linux-x64-sysv" => new
            {
                abi = "linux-x64-sysv",
                description = "Linux x86-64 System V ABI",
                argument_registers = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" },
                return_register = "rax",
                caller_saved = new[] { "rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11" },
                callee_saved = new[] { "rbx", "rbp", "r12", "r13", "r14", "r15" },
                stack_alignment = "RSP ≡ 0 (mod 16) before call",
                max_args_in_registers = 6
            },
            "windows-x64-msabi" => new
            {
                abi = "windows-x64-msabi",
                description = "Microsoft x64 calling convention",
                argument_registers = new[] { "rcx", "rdx", "r8", "r9" },
                return_register = "rax",
                caller_saved = new[] { "rax", "rcx", "rdx", "r8", "r9", "r10", "r11" },
                callee_saved = new[] { "rbx", "rbp", "rdi", "rsi", "r12", "r13", "r14", "r15" },
                stack_alignment = "RSP ≡ 8 (mod 16) before call, 32-byte shadow space",
                max_args_in_registers = 4
            },
            _ => throw new ArgumentException($"Unknown target '{target}'. Supported: linux-x64-sysv, windows-x64-msabi")
        };

        return JsonSerializer.Serialize(info, JsonOpts);
    }

    [McpServerTool, Description("Generate a C header file for exported procedures")]
    public static string GenerateHeader(
        string source,
        string? libraryName = null)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file '{source}' not found.");

        var sourceText = File.ReadAllText(source);
        var libName = libraryName ?? Path.GetFileNameWithoutExtension(source);
        return HlaX64.Cli.CodeGen.InteropGenerator.GenerateCHeader(sourceText, libName);
    }

    [McpServerTool, Description("Generate C# P/Invoke declarations for exported procedures")]
    public static string GeneratePInvoke(
        string source,
        string? libraryName = null)
    {
        if (!File.Exists(source))
            throw new FileNotFoundException($"Source file '{source}' not found.");

        var sourceText = File.ReadAllText(source);
        var libName = libraryName ?? Path.GetFileNameWithoutExtension(source);
        return HlaX64.Cli.CodeGen.InteropGenerator.GeneratePInvoke(sourceText, libName);
    }

    [McpServerTool, Description("Get the compiler version")]
    public static string GetVersion()
    {
        return Compilation.GetVersion();
    }

    [McpServerTool, Description("List available instruction mnemonics supported by the compiler")]
    public static string ListInstructions()
    {
        var instructions = new[]
        {
            "mov", "add", "sub", "imul", "xor", "and", "or", "cmp",
            "jmp", "je", "jne", "jg", "jl", "jge", "jle", "ja", "jb", "jae", "jbe",
            "call", "ret"
        };
        return JsonSerializer.Serialize(new { instructions }, JsonOpts);
    }
}
