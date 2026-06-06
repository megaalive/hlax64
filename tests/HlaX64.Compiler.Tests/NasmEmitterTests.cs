using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Tests;

public class NasmEmitterTests
{
    private string Emit(string source)
    {
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();
        var emitter = new NasmEmitter();
        return emitter.Emit(program);
    }

    [Fact]
    public void Emit_SimpleProgram_ContainsGlobalStart()
    {
        var nasm = Emit("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        Assert.Contains("global _start", nasm);
        Assert.Contains("_start:", nasm);
        Assert.Contains("section .text", nasm);
        Assert.Contains("section .data", nasm);
    }

    [Fact]
    public void Emit_MovInstruction_OperandOrderReversed()
    {
        var nasm = Emit("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        Assert.Contains("mov rax, 1", nasm);
    }

    [Fact]
    public void Emit_AddInstruction_OperandOrderReversed()
    {
        var nasm = Emit("program test;\nbegin test;\n    add(2, rax);\nend test;");
        Assert.Contains("add rax, 2", nasm);
    }

    [Fact]
    public void Emit_StdoutPutString_GeneratesStringData()
    {
        var nasm = Emit("program test;\nbegin test;\n    stdout.put(\"Hello\", nl);\nend test;");
        Assert.Contains("str_0 db \"Hello\", 0", nasm);
        Assert.Contains("mov rax, 1", nasm);
        Assert.Contains("lea rsi, [str_0]", nasm);
        Assert.Contains("mov rdx, 5", nasm);
        Assert.Contains("lea rsi, [newline]", nasm);
        Assert.Contains("mov rdx, 1", nasm);
    }

    [Fact]
    public void Emit_StdoutPutInteger_GeneratesStringData()
    {
        var nasm = Emit("program test;\nbegin test;\n    stdout.put(42, nl);\nend test;");
        Assert.Contains("str_0 db \"42\", 0", nasm);
        Assert.Contains("mov rdx, 2", nasm);
    }

    [Fact]
    public void Emit_StdoutPutRegister_GeneratesDecimalConversion()
    {
        var nasm = Emit("program test;\nbegin test;\n    stdout.put(rax, nl);\nend test;");
        Assert.Contains(".Ldiv_0:", nasm);
        Assert.Contains(".Lchk_0:", nasm);
        Assert.Contains(".Lascii_0:", nasm);
    }

    [Fact]
    public void Emit_IfElse_GeneratesLabels()
    {
        var nasm = Emit("program test;\nbegin test;\nif(rax = 0) then\n    mov(1, rbx);\nelse\n    mov(2, rbx);\nendif;\nend test;");
        Assert.Contains("cmp rax, 0", nasm);
        Assert.Contains("jne else_0", nasm);
        Assert.Contains("jmp endif_0", nasm);
        Assert.Contains("else_0:", nasm);
        Assert.Contains("endif_0:", nasm);
    }

    [Fact]
    public void Emit_WhileLoop_GeneratesLabels()
    {
        var nasm = Emit("program test;\nbegin test;\nwhile(rax < 10) do\n    add(1, rax);\nendwhile;\nend test;");
        Assert.Contains("while_0:", nasm);
        Assert.Contains("cmp rax, 10", nasm);
        Assert.Contains("jge endwhile_0", nasm);
        Assert.Contains("jmp while_0", nasm);
        Assert.Contains("endwhile_0:", nasm);
    }

    [Fact]
    public void Emit_Procedure_ParameterResolvesToStackOffset()
    {
        var source = "program test;\nprocedure AddTwo(a:int64; b:int64); @returns(\"rax\");\nbegin AddTwo;\n    mov(a, rax);\n    add(b, rax);\nend AddTwo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = Emit(source);
        Assert.Contains("AddTwo:", nasm);
        Assert.Contains("push rbp", nasm);
        Assert.Contains("mov rbp, rsp", nasm);
        Assert.Contains("mov [rbp-8], rdi", nasm);
        Assert.Contains("mov [rbp-16], rsi", nasm);
        // mov(a, rax) -> mov rax, [rbp-8] (reversed operands)
        Assert.Contains("mov rax, [rbp-8]", nasm);
        // add(b, rax) -> add rax, [rbp-16] (reversed operands)
        Assert.Contains("add rax, [rbp-16]", nasm);
        Assert.Contains("pop rbp", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_ProcedureWithVariables_StackLayout()
    {
        var source = "program test;\nprocedure Foo(x:int64); @returns(\"rax\");\nvar\n    total:int64;\nbegin Foo;\n    mov(x, rax);\nend Foo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = Emit(source);
        // 1 variable = sub rsp, 8
        Assert.Contains("sub rsp, 8", nasm);
        // Parameter stored from rdi
        Assert.Contains("mov [rbp-8], rdi", nasm);
        // mov(x, rax) -> mov rax, [rbp-8] (reversed operands)
        Assert.Contains("mov rax, [rbp-8]", nasm);
    }

    [Fact]
    public void Emit_ProcedureWithReturnAndVariables()
    {
        var source = "program test;\nprocedure Sum(a:int64; b:int64); @returns(\"rax\");\nvar\n    result:int64;\nbegin Sum;\n    mov(a, rax);\n    add(b, rax);\nend Sum;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = Emit(source);
        Assert.Contains("Sum:", nasm);
        Assert.Contains("mov [rbp-8], rdi", nasm);
        Assert.Contains("mov [rbp-16], rsi", nasm);
        Assert.Contains("mov rax, [rbp-8]", nasm);
        Assert.Contains("add rax, [rbp-16]", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_ExitSyscall_UsesRaxAsExitCode()
    {
        var nasm = Emit("program test;\nbegin test;\n    mov(42, rax);\nend test;");
        Assert.Contains("xor ebx, ebx", nasm);
        Assert.Contains("mov rax, 60", nasm);
        Assert.Contains("syscall", nasm);
    }

    [Fact]
    public void Emit_ComplexProgram_ContainsAllParts()
    {
        var source = @"program hello;

#include(""stdlib64.hhf"")

begin hello;
    stdout.put(""Hello from HlaX64"", nl);
end hello;";
        var nasm = Emit(source);
        Assert.Contains("str_0 db \"Hello from HlaX64\", 0", nasm);
        Assert.Contains("lea rsi, [str_0]", nasm);
        Assert.Contains("lea rsi, [newline]", nasm);
        Assert.Contains("global _start", nasm);
    }

    // -----------------------------------------------------------------
    // Fase 4 — Runtime Minimal: stdout.put
    // -----------------------------------------------------------------

    [Fact]
    public void Emit_StdoutPutString_ContainsRuntimeComment()
    {
        var nasm = Emit("program t;\nbegin t;\n    stdout.put(\"hi\", nl);\nend t;");
        Assert.Contains("; RUNTIME: stdout_put_str", nasm);
    }

    [Fact]
    public void Emit_StdoutPutNl_ContainsRuntimeComment()
    {
        var nasm = Emit("program t;\nbegin t;\n    stdout.put(nl);\nend t;");
        Assert.Contains("; RUNTIME: stdout_put_nl", nasm);
    }

    [Fact]
    public void Emit_StdoutPutRegister_ContainsRuntimeComment()
    {
        var nasm = Emit("program t;\nbegin t;\n    stdout.put(rax, nl);\nend t;");
        Assert.Contains("; RUNTIME: stdout_put_int(rax)", nasm);
    }

    [Fact]
    public void Emit_StdoutPutIntegerLiteral_ContainsRuntimeComment()
    {
        var nasm = Emit("program t;\nbegin t;\n    stdout.put(42, nl);\nend t;");
        Assert.Contains("; RUNTIME: stdout_put_str (constant int literal)", nasm);
    }

    [Fact]
    public void Emit_IncludeDirective_DoesNotEmitAndDoesNotCrash()
    {
        // #include is documentation-only for MVP. The AST contains an
        // IncludeNode but the emitter must silently drop it.
        var nasm = Emit("program t;\n#include(\"stdlib64.hhf\")\nbegin t;\n    mov(1, rax);\nend t;");
        // No "include" or "stdlib64" should leak into the output.
        Assert.DoesNotContain("include", nasm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stdlib64", nasm, StringComparison.OrdinalIgnoreCase);
        // The actual instruction still makes it through.
        Assert.Contains("mov rax, 1", nasm);
    }

    [Fact]
    public void Emit_HelloWorldSample_GeneratesRunnableNasm()
    {
        // Use the same source as the actual sample under tests/samples/hello/
        var source = File.ReadAllText(GetSamplePath("hello", "hello.hla64"));
        var nasm = Emit(source);

        // Required structural pieces for a runnable Linux x64 ELF program.
        Assert.Contains("bits 64", nasm);
        Assert.Contains("section .text", nasm);
        Assert.Contains("section .data", nasm);
        Assert.Contains("global _start", nasm);
        Assert.Contains("_start:", nasm);
        Assert.Contains("sys_exit", nasm);

        // String literal and newline must be present in .data.
        Assert.Contains("str_0 db \"Hello from HlaX64\", 0", nasm);
        Assert.Contains("newline db 0x0A", nasm);

        // The sys_write sequence for the literal must be present.
        Assert.Contains("lea rsi, [str_0]", nasm);
        Assert.Contains("mov rdx, 17", nasm); // length of "Hello from HlaX64"

        // RUNTIME markers for both string and newline.
        Assert.Contains("; RUNTIME: stdout_put_str", nasm);
        Assert.Contains("; RUNTIME: stdout_put_nl", nasm);
    }

    [Fact]
    public void Emit_ExitCodeSample_GeneratesExpectedExitSyscall()
    {
        // The exitcode sample should set rdi = 42 for the exit code.
        var source = File.ReadAllText(GetSamplePath("exitcode", "exitcode.hla64"));
        var nasm = Emit(source);
        Assert.Contains("mov rbx, 42", nasm);
        Assert.Contains("mov rax, 60", nasm);
        Assert.Contains("syscall", nasm);
    }

    private static string GetSamplePath(string sampleDir, string fileName)
    {
        // Walk up from the test bin folder until we find the repo's
        // tests/samples/<dir>/<file>.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "samples", sampleDir, fileName);
            if (File.Exists(candidate))
                return candidate;

            // Also try from src/<proj>/bin/Debug/netX.Y to repo root.
            candidate = Path.Combine(dir.FullName, "..", "..", "..", "..", "tests", "samples", sampleDir, fileName);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"Could not locate sample {sampleDir}/{fileName} starting from {AppContext.BaseDirectory}");
    }
}
