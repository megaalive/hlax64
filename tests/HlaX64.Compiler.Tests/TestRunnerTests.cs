using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using System.Text.Json;

namespace HlaX64.Compiler.Tests;

public class TestRunnerTests
{
    private static string CompileToNasm(string source)
    {
        var compilation = new Compiler.Compilation("(test)", source);
        var result = compilation.Process();
        if (!result.Success)
            throw new InvalidOperationException(string.Join("; ", result.Diagnostics));
        var emitter = new NasmEmitter();
        return emitter.Emit(result.LoweredFunctions, result.StringLiterals);
    }

    [Fact]
    public void TestManifest_LoadFromJson_ParsesCorrectly()
    {
        var manifest = new TestManifest
        {
            Name = "test1",
            Source = "test.hla64",
            ExpectedExitCode = 0,
            ExpectedStdout = "hello\\n",
            Description = "Test program"
        };

        var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var tempFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempFile, json);
            var loaded = TestManifest.LoadFromJson(tempFile);
            Assert.Equal("test1", loaded.Name);
            Assert.Equal("test.hla64", loaded.Source);
            Assert.Equal(0, loaded.ExpectedExitCode);
            Assert.Equal("hello\\n", loaded.ExpectedStdout);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TestRunner_CompileOnly_Succeeds()
    {
        var runner = new TestRunner(compileFunc: CompileToNasm, skipExecution: true);
        var manifest = new TestManifest
        {
            Name = "simple",
            Source = "examples/01-arithmetic/simple.hla64"
        };

        var buildDir = Path.Combine(Path.GetTempPath(), "hlax64_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = runner.RunTest(manifest, buildDir);
            Assert.True(result.Passed, result.ErrorMessage);
            Assert.True(File.Exists(Path.Combine(buildDir, "simple.nasm")));
        }
        finally
        {
            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
        }
    }

    [Fact]
    public void TestRunner_MissingSource_ReturnsError()
    {
        var runner = new TestRunner(compileFunc: CompileToNasm, skipExecution: true);
        var manifest = new TestManifest
        {
            Name = "nonexistent",
            Source = "nonexistent_file.hla64"
        };

        var buildDir = Path.Combine(Path.GetTempPath(), "hlax64_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = runner.RunTest(manifest, buildDir);
            Assert.False(result.Passed);
            Assert.Contains("not found", result.ErrorMessage!);
        }
        finally
        {
            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
        }
    }

    [Fact]
    public void TestRunner_NoCompileFunc_ReturnsError()
    {
        var runner = new TestRunner(skipExecution: true);
        var manifest = new TestManifest
        {
            Name = "test",
            Source = "examples/01-arithmetic/simple.hla64"
        };

        var buildDir = Path.Combine(Path.GetTempPath(), "hlax64_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = runner.RunTest(manifest, buildDir);
            Assert.False(result.Passed);
            Assert.Contains("No compile function", result.ErrorMessage!);
        }
        finally
        {
            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
        }
    }

    [Fact]
    public void TestRunner_CompileError_ReturnsCompileFailed()
    {
        var runner = new TestRunner(compileFunc: CompileToNasm, skipExecution: true);
        var tempFile = Path.GetTempFileName() + ".hla64";
        try
        {
            File.WriteAllText(tempFile, "this is not valid hla code !!!");
            var manifest = new TestManifest
            {
                Name = "bad",
                Source = tempFile
            };

            var buildDir = Path.Combine(Path.GetTempPath(), "hlax64_test_" + Guid.NewGuid().ToString("N")[..8]);
            try
            {
                var result = runner.RunTest(manifest, buildDir);
                Assert.False(result.Passed);
                Assert.True(result.CompileFailed);
                Assert.Contains("Compilation failed", result.ErrorMessage!);
            }
            finally
            {
                if (Directory.Exists(buildDir))
                    Directory.Delete(buildDir, true);
            }
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void TestRunner_HelloWorld_CompilesAndWritesNasm()
    {
        var runner = new TestRunner(compileFunc: CompileToNasm, skipExecution: true);
        var manifest = new TestManifest
        {
            Name = "hello",
            Source = "examples/00-getting-started/hello.hla64"
        };

        var buildDir = Path.Combine(Path.GetTempPath(), "hlax64_test_" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var result = runner.RunTest(manifest, buildDir);
            Assert.True(result.Passed, result.ErrorMessage);

            var nasmContent = File.ReadAllText(Path.Combine(buildDir, "hello.nasm"));
            Assert.Contains("Hello from HlaX64", nasmContent);
            Assert.Contains("global _start", nasmContent);
            Assert.Contains("sys_write", nasmContent);
        }
        finally
        {
            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
        }
    }

    [Fact]
    public void TestResult_ToString_PassFormat()
    {
        var result = new TestResult
        {
            Name = "test",
            Passed = true,
            Duration = TimeSpan.FromMilliseconds(42)
        };
        Assert.Contains("PASS: test", result.ToString());
        Assert.Contains("42ms", result.ToString());
    }

    [Fact]
    public void TestResult_ToString_FailFormat()
    {
        var result = new TestResult
        {
            Name = "test",
            Passed = false,
            ErrorMessage = "exit code mismatch",
            ActualExitCode = 1
        };
        Assert.Contains("FAIL: test", result.ToString());
        Assert.Contains("exit code mismatch", result.ToString());
    }

    [Fact]
    public void TestManifest_LoadAll_FindsJsonFiles()
    {
        var manifests = TestManifest.LoadAll("tests/samples");
        Assert.NotEmpty(manifests);
        Assert.Contains(manifests, m => m.Name == "exitcode");
        Assert.Contains(manifests, m => m.Name == "comparison_uint64_boundary");
    }
}