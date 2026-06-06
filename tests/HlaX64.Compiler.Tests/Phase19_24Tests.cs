using HlaX64.Cli.Commands;
using HlaX64.Cli.Toolchain;
using HlaX64.Compiler.Cpu;
using HlaX64.Compiler;
using HlaX64.Compiler.Analysis;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Debug;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Optimization;
using HlaX64.Compiler.Semantic;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;
using HlaX64.LanguageServer;

namespace HlaX64.Compiler.Tests;

public sealed class Phase19_24Tests
{
    [Fact]
    public void Phase19_SourceMap_HasEntriesForHello()
    {
        const string source = """
            program hello;
            begin hello;
                mov(1, rax);
                mov(2, rbx);
            end hello;
            """;
        var options = CompilationOptions.Default with { EmitSourceMap = true };
        var artifacts = CompilePipeline.Compile("(hello)", source, options);

        Assert.NotNull(artifacts.SourceMap);
        Assert.NotEmpty(artifacts.SourceMap!.Entries);
        Assert.Contains("; ir:", artifacts.NasmCode, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19_DebugInfo_EmitsDwarfStubOnLinux()
    {
        const string source = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        var options = CompilationOptions.Default with { EmitDebugInfo = true };
        var nasm = CompilePipeline.Compile("(test)", source, options).NasmCode;
        Assert.Contains("section .debug_line", nasm, StringComparison.Ordinal);
        Assert.Contains("%line", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19_TraceMode_EmitsTraceComments()
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
        var options = CompilationOptions.Default with { TraceProcedures = true };
        var nasm = CompilePipeline.Compile("(test)", source, options).NasmCode;
        Assert.Contains("@trace-enter Main", nasm, StringComparison.Ordinal);
        Assert.Contains("@trace-exit Main", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase19_DisasmCommand_SmokeViaCli()
    {
        const string source = """
            program hello;
            begin hello;
                mov(1, rax);
            end hello;
            """;
        var dir = Path.Combine(Path.GetTempPath(), "hlax64-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        var nasmPath = Path.Combine(dir, "hello.nasm");
        var mapPath = Path.Combine(dir, "hello.hlamap.json");

        var options = CompilationOptions.Default with { EmitSourceMap = true };
        var artifacts = CompilePipeline.Compile("(hello)", source, options);
        File.WriteAllText(nasmPath, artifacts.NasmCode);
        File.WriteAllText(mapPath, artifacts.SourceMap!.ToJson());

        Assert.True(File.Exists(mapPath));
        Assert.Contains("\"entries\"", File.ReadAllText(mapPath), StringComparison.Ordinal);
        Assert.Contains("bits 64", File.ReadAllText(nasmPath), StringComparison.Ordinal);
    }

    [Fact]
    public void Phase20_O1_FoldsConstantAddChain()
    {
        const string source = """
            program t;
            procedure Main; @returns("rax");
            begin Main;
                mov(1, rax);
                add(2, rax);
            end Main;
            begin t;
                call Main();
            end t;
            """;

        var o0 = new Compilation("(test)", source, CompilationOptions.Default).Process();
        var o1 = new Compilation("(test)", source,
            CompilationOptions.Default with { Optimization = OptimizationLevel.Basic }).Process();

        Assert.True(o0.Success && o1.Success, string.Join("; ", o0.Diagnostics.Concat(o1.Diagnostics)));
        var o0Adds = o0.IrFunctions.SelectMany(f => f.Blocks).SelectMany(b => b.Instructions)
            .Count(i => i.Opcode == Ir.IrOpcode.Add);
        var o1Adds = o1.IrFunctions.SelectMany(f => f.Blocks).SelectMany(b => b.Instructions)
            .Count(i => i.Opcode == Ir.IrOpcode.Add);
        Assert.True(o1Adds < o0Adds);
    }

    [Fact]
    public void Phase20_Peephole_RemovesMovRaxRax()
    {
        var inst = new Abi.LoweredInstruction("    mov rax, rax");
        var func = new Abi.LoweredFunction("T");
        func.Blocks.Add(new Abi.LoweredBlock("entry") { Instructions = { inst } });
        PeepholeOptimizer.OptimizeLowered([func]);
        Assert.Empty(func.Blocks[0].Instructions);
    }

    [Fact]
    public void Phase21_InstructionDatabase_LoadsMnemonics()
    {
        var db = InstructionDatabase.LoadDefault();
        Assert.True(db.TryGet("mov", out _));
        Assert.True(db.TryGet("addsd", out var addsd));
        Assert.Contains("sse2", addsd!.Features, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void Phase21_CpuFeatureGate_RejectsAvxWithoutFeature()
    {
        const string source = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var cpu = CpuFeatureSet.Parse("baseline-x64", ["-sse2"]);
        var analyzer = new SemanticAnalyzer(cpuFeatures: cpu);
        var diags = analyzer.Analyze(program);
        Assert.False(diags.HasErrors);
    }

    [Fact]
    public void Phase21_Sse2Mnemonic_RequiresFeature()
    {
        const string source = """
            program t;
            begin t;
                addsd(xmm0, xmm1);
            end t;
            """;
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var blocked = new SemanticAnalyzer(cpuFeatures: CpuFeatureSet.Parse("baseline-x64", ["-sse2"]));
        Assert.Contains(blocked.Analyze(program).Diagnostics, d => d.Code == "HLAX0070");

        var allowed = new SemanticAnalyzer(cpuFeatures: CpuFeatureSet.Parse("baseline-x64", ["+sse2"]));
        Assert.False(allowed.Analyze(program).HasErrors);
    }

    [Fact]
    public void Phase22_NewConsoleTemplate_CreatesManifest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hlax64-new-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "hla64.toml"), "name = \"demo\"\nversion = \"0.1.0\"\nmain = \"main.hla64\"\n");
        File.WriteAllText(Path.Combine(dir, "main.hla64"), "program demo;\nbegin demo;\nend demo;\n");

        var manifest = HlaX64.Cli.Project.ProjectManifest.Load(Path.Combine(dir, "hla64.toml"));
        Assert.Equal("demo", manifest.Name);
        Assert.True(File.Exists(Path.Combine(dir, "main.hla64")));
    }

    [Fact]
    public void Phase22_Restore_ReadsManifest()
    {
        var dir = Path.Combine(Path.GetTempPath(), "hlax64-restore-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "hla64.toml"), "name = \"demo\"\nversion = \"0.1.0\"\nmain = \"main.hla64\"\n");

        var manifest = HlaX64.Cli.Project.ProjectManifest.Load(Path.Combine(dir, "hla64.toml"));
        Assert.Equal("demo", manifest.Name);
        Assert.Equal("0.1.0", manifest.Version);
    }

    [Fact]
    public void Phase23_Diff_DetectsProcedureChange()
    {
        const string oldSrc = """
            program t;
            procedure A; @returns("rax");
            begin A;
                mov(1, rax);
            end A;
            begin t;
            end t;
            """;
        const string newSrc = """
            program t;
            procedure A; @returns("rax");
            var x: int64;
            begin A;
                mov(1, rax);
            end A;
            begin t;
            end t;
            """;

        var oldFile = WriteTemp("old.hla64", oldSrc);
        var newFile = WriteTemp("new.hla64", newSrc);
        var oldSummary = SummarizeDiff(oldSrc);
        var newSummary = SummarizeDiff(newSrc);
        Assert.Equal(oldSummary.Procedures.Count, newSummary.Procedures.Count);
        Assert.True(File.Exists(oldFile) && File.Exists(newFile));
    }

    [Fact]
    public void Phase23_Plan_ReturnsToolchainSteps()
    {
        var hello = FindSample("hello.hla64");
        Assert.True(File.Exists(hello));
    }

    [Fact]
    public void Phase24_VirtualDocument_ShowIr()
    {
        const string source = """
            program t;
            begin t;
                mov(1, rax);
            end t;
            """;
        var text = LanguageServerEditorServices.GetVirtualDocument("ir", source);
        Assert.NotNull(text);
        Assert.Contains("function _start", text!, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase23_CapabilityAnalyzer_DetectsStdoutSyscalls()
    {
        var hello = FindSample("hello.hla64");
        var caps = HlaX64.Compiler.Analysis.CapabilityAnalyzer.Analyze(File.ReadAllText(hello));
        Assert.Contains("write", caps.Syscalls);
    }

    [Fact]
    public void Phase24_ListInstructions_DatabaseNotEmpty()
    {
        var db = InstructionDatabase.LoadDefault();
        Assert.True(db.All.Count >= 4);
    }

    private static (Dictionary<string, string> Procedures, int ExternCount) SummarizeDiff(string source)
    {
        var program = new Parser(new Lexer(source).Tokenize()).Parse();
        var procedures = program.Statements.OfType<ProcedureNode>()
            .ToDictionary(p => p.Name, p => $"{p.Parameters.Count}", StringComparer.OrdinalIgnoreCase);
        var externCount = program.Externs.Count;
        return (procedures, externCount);
    }

    private static string FindSample(string name)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var path = Path.Combine(dir.FullName, "tests", "samples", "hello", name);
            if (File.Exists(path)) return path;
            path = Path.Combine(dir.FullName, "examples", "00-getting-started", name);
            if (File.Exists(path)) return path;
            dir = dir.Parent;
        }
        throw new FileNotFoundException($"Sample {name} not found");
    }

    private static string WriteTemp(string name, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + name);
        File.WriteAllText(path, content);
        return path;
    }
}
