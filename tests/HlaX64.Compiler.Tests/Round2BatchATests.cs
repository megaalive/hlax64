using HlaX64.Cli.Commands;
using HlaX64.Compiler;
using HlaX64.Compiler.Cpu;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Tests;

public sealed class Round2BatchATests
{
    [Fact]
    public void Avx2Inline_LowersToYmmInstructions()
    {
        const string source = """
            program t;
            begin t;
                vaddpd(ymm0, ymm1);
                vmovapd(ymm2, ymm3);
                vxorpd(ymm0, ymm1);
            end t;
            """;
        var options = CompilationOptions.Default with
        {
            CpuFeatures = CpuFeatureSet.Parse("baseline-x64", ["+avx2"])
        };
        var nasm = CompilePipeline.Compile("(test)", source, options).NasmCode;
        Assert.Contains("vaddpd ymm1, ymm0", nasm, StringComparison.Ordinal);
        Assert.Contains("vmovapd ymm3, ymm2", nasm, StringComparison.Ordinal);
        Assert.Contains("vxorpd ymm1, ymm1, ymm0", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void SimdIntrinsics_LowerWhenAvx2Enabled()
    {
        const string source = """
            program t;
            begin t;
                simd.add_f64x4(ymm0, ymm1);
                simd.load_f64x4(rax);
                simd.store_f64x4(rax, ymm0);
            end t;
            """;
        var options = CompilationOptions.Default with
        {
            CpuFeatures = CpuFeatureSet.Parse("baseline-x64", ["+avx2"])
        };
        var nasm = CompilePipeline.Compile("(test)", source, options).NasmCode;
        Assert.Contains("vaddpd ymm0, ymm1", nasm, StringComparison.Ordinal);
        Assert.Contains("vmovapd ymm0, [rax]", nasm, StringComparison.Ordinal);
        Assert.Contains("vmovapd [rax], ymm0", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void SimdIntrinsics_RequireAvx2Feature()
    {
        const string source = """
            program t;
            begin t;
                simd.add_f64x4(ymm0, ymm1);
            end t;
            """;
        var program = new HlaX64.Compiler.Parsing.Parser(
            new HlaX64.Compiler.Lexing.Lexer(source).Tokenize()).Parse();
        var blocked = new SemanticAnalyzer(cpuFeatures: CpuFeatureSet.Parse("baseline-x64", ["-avx2"]));
        Assert.Contains(blocked.Analyze(program).Diagnostics, d => d.Code == "HLAX0070");
    }

    [Fact]
    public void Atomics_LowerWithLockAndBarriers()
    {
        const string source = """
            program t;
            procedure Main; @returns("rax");
            var counter: int64;
            begin Main;
                atomic.load(&counter, relaxed);
                atomic.store(&counter, 1, release);
                atomic.fetch_add(&counter, 5, acq_rel);
            end Main;
            begin t;
                call Main();
            end t;
            """;
        var nasm = CompilePipeline.Compile("(test)", source).NasmCode;
        Assert.Contains("mov rax,", nasm, StringComparison.Ordinal);
        Assert.Contains("lock xadd", nasm, StringComparison.Ordinal);
        Assert.Contains("mfence", nasm, StringComparison.Ordinal);
    }

    [Fact]
    public void AtomicOrdering_InvalidReportsHlax0073()
    {
        const string source = """
            program t;
            procedure Main; @returns("rax");
            var x: int64;
            begin Main;
                atomic.load(&x, bogus);
            end Main;
            begin t;
            end t;
            """;
        var program = new HlaX64.Compiler.Parsing.Parser(
            new HlaX64.Compiler.Lexing.Lexer(source).Tokenize()).Parse();
        var diags = new SemanticAnalyzer().Analyze(program);
        Assert.Contains(diags.Diagnostics, d => d.Code == "HLAX0073");
    }
}
