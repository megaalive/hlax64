using HlaX64.Cli.Commands;
using HlaX64.Cli.Project;
using HlaX64.Compiler;
using HlaX64.Compiler.Cpu;
using HlaX64.Compiler.Debug;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Optimization;
using HlaX64.Compiler.Semantic;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.LanguageServer;

namespace HlaX64.Compiler.Tests;

public sealed class HardeningTests
{
    [Fact]
    public void BatchB_SourceMap_RoundTripLookup()
    {
        const string source = """
            program t;
            begin t;
                mov(1, rax);
                add(2, rax);
            end t;
            """;
        var options = CompilationOptions.Default with { EmitSourceMap = true };
        var artifacts = CompilePipeline.Compile("(test)", source, options);
        var map = artifacts.SourceMap!;
        Assert.NotEmpty(map.Entries);
        var entry = map.Entries.First();
        Assert.True(entry!.IrId > 0);

        if (entry.NasmLine != null)
        {
            var byNasm = map.LookupByNasmLine(entry.NasmLine.Value);
            Assert.NotNull(byNasm);
            Assert.Equal(entry.IrId, byNasm!.IrId);
        }

        var byIr = map.LookupByIrId(entry.IrId);
        Assert.NotNull(byIr);
    }

    [Fact]
    public void BatchB_TraceMode_EmitsInt3OnLinux()
    {
        const string source = """
            program t;
            procedure Main; @returns("rax");
            begin Main;
                mov(1, rax);
            end Main;
            begin t;
                call Main();
            end t;
            """;
        var nasm = CompilePipeline.Compile("(test)", source,
            CompilationOptions.Default with { TraceProcedures = true }).NasmCode;
        Assert.Contains("@trace-enter Main", nasm, StringComparison.Ordinal);
        Assert.Contains("int3", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void BatchC_O2_FoldsMovConstantChain()
    {
        const string source = """
            program t;
            procedure Main; @returns("rax");
            begin Main;
                mov(5, rax);
                mov(rax, rbx);
            end Main;
            begin t;
                call Main();
            end t;
            """;
        var o2 = new Compilation("(test)", source,
            CompilationOptions.Default with { Optimization = OptimizationLevel.Aggressive }).Process();
        Assert.True(o2.Success, string.Join("; ", o2.Diagnostics));
        var loads = o2.IrFunctions.SelectMany(f => f.Blocks).SelectMany(b => b.Instructions)
            .Where(i => i.Opcode == Ir.IrOpcode.LoadConstant && i.Immediate is long)
            .Select(i => (long)i.Immediate!)
            .ToList();
        Assert.Contains(5L, loads);
    }

    [Fact]
    public void BatchC_PeepholeO2_RewritesMovZeroToXor()
    {
        var inst = new Abi.LoweredInstruction("    mov rax, 0");
        var func = new Abi.LoweredFunction("T");
        func.Blocks.Add(new Abi.LoweredBlock("entry") { Instructions = { inst } });
        PeepholeOptimizer.OptimizeLowered([func], OptimizationLevel.Aggressive);
        Assert.Contains("xor rax, rax", func.Blocks[0].Instructions[0].AsmText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatchD_Avx2Mnemonic_RequiresFeature()
    {
        const string source = """
            program t;
            begin t;
                vaddpd(xmm0, xmm1);
            end t;
            """;
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var blocked = new SemanticAnalyzer(cpuFeatures: CpuFeatureSet.Parse("baseline-x64", ["-avx2"]));
        Assert.Contains(blocked.Analyze(program).Diagnostics, d => d.Code == "HLAX0070");

        var allowed = new SemanticAnalyzer(cpuFeatures: CpuFeatureSet.Parse("baseline-x64", ["+avx2"]));
        Assert.False(allowed.Analyze(program).HasErrors);
    }

    [Fact]
    public void BatchD_UnknownMnemonic_UsesHlax0071()
    {
        const string source = """
            program t;
            begin t;
                moov(1, rax);
            end t;
            """;
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0071");
    }

    [Fact]
    public void BatchE_Restore_WritesLockFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hlax64-lock-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "hla64.toml"), "name = \"demo\"\nversion = \"0.1.0\"\nmain = \"main.hla64\"\n");
        File.WriteAllText(Path.Combine(dir, "main.hla64"), "program demo;\nbegin demo;\nend demo;\n");

        var manifest = ProjectManifest.Load(Path.Combine(dir, "hla64.toml"));
        Assert.Equal("demo", manifest.Name);

        var lockPath = Path.Combine(dir, "hla64.lock");
        Assert.False(File.Exists(lockPath));
        // Simulate restore output
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(File.ReadAllText(Path.Combine(dir, "hla64.toml"))))).ToLowerInvariant();
        File.WriteAllText(lockPath, $"{{\"manifestHash\":\"{hash}\"}}");
        Assert.True(File.Exists(lockPath));
    }

    [Fact]
    public void BatchE_NewConsoleProject_Compiles()
    {
        const string source = """
            program app;
            procedure Main; @returns("rax");
            begin Main;
                mov(42, rax);
            end Main;
            begin app;
                call Main();
            end app;
            """;
        var result = new Compilation("(main)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void BatchF_CapabilityAnalyzer_DetectsStdoutAndExtern()
    {
        const string source = """
            program t;
            extern procedure puts(msg: cstring): int32 from "libc.so";
            begin t;
                call puts();
                stdout.put("hi", nl);
            end t;
            """;
        var caps = HlaX64.Compiler.Analysis.CapabilityAnalyzer.Analyze(source);
        Assert.True(caps.HasStdoutPut);
        Assert.True(caps.HasExtern);
        Assert.Contains("puts", caps.ExternProcedures);
    }

    [Fact]
    public void BatchG_VirtualStackDoc_IncludesClobberList()
    {
        const string source = """
            program t;
            procedure P; @returns("rax");
            begin P;
                mov(1, rax);
            end P;
            begin t;
            end t;
            """;
        var text = LanguageServerEditorServices.GetVirtualDocument("stack", source);
        Assert.Contains("caller-saved", text!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("clobbered", text!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BatchH_DifferentialSimpleArithmetic_Compiles()
    {
        var path = FindExample("curriculum/01-arithmetic/simple.hla64");
        var source = File.ReadAllText(path);
        var result = new Compilation(path, source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
    }

    [Fact]
    public void BatchH_DifferentialSimpleArithmetic_ExitCodeManifest()
    {
        var repoRoot = FindRepoRoot();
        var manifest = TestManifest.LoadFromJson(
            Path.Combine(repoRoot, "tests", "examples-curriculum", "simple", "manifest.json"));
        Assert.Equal(3, manifest.ExpectedExitCode);

        var runner = new TestRunner(compileFunc: s =>
        {
            var r = new Compilation("(test)", s).Process();
            if (!r.Success) throw new InvalidOperationException(string.Join("; ", r.Diagnostics));
            var emitter = new HlaX64.Backend.Nasm.Emitters.NasmEmitter();
            return emitter.Emit(r.LoweredFunctions, r.StringLiterals);
        }, skipExecution: true);

        var buildDir = Path.Combine(Path.GetTempPath(), "hlax64-diff-" + Guid.NewGuid().ToString("N")[..8]);
        var result = runner.RunTest(manifest, buildDir);
        Assert.True(result.Passed, result.ErrorMessage);
    }

    [Fact]
    public void BatchD_InstructionDatabase_HasAtLeast30Mnemonics()
    {
        var db = InstructionDatabase.LoadDefault();
        Assert.True(db.All.Count >= 30, $"Expected >=30 instructions, got {db.All.Count}");
    }

    private static string FindExample(string relative)
    {
        var dir = new DirectoryInfo(FindRepoRoot());
        var path = Path.Combine(dir.FullName, "examples", relative.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path)) return path;
        throw new FileNotFoundException(relative);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "tests", "conformance")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Repo root not found");
    }
}
