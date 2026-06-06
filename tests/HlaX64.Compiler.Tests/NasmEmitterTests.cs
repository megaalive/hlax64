using HlaX64.Backend.Nasm.Emitters;
using HlaX64.Compiler;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Tests;

public class NasmEmitterTests
{
    private static string Emit(string source)
    {
        var compilation = new Compilation("(test)", source);
        var result = compilation.Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var emitter = new NasmEmitter();
        return emitter.Emit(result.LoweredFunctions, result.StringLiterals);
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
        Assert.Contains("lea rsi, [newline]", nasm);
        Assert.Contains("mov rdx, 1", nasm);
    }

    [Fact]
    public void Emit_StdoutPutInteger_GeneratesStringData()
    {
        var nasm = Emit("program test;\nbegin test;\n    stdout.put(42, nl);\nend test;");
        Assert.Contains("str_0 db \"42\", 0", nasm);
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
        Assert.Contains("je then_0", nasm);
        Assert.Contains("jmp else_0", nasm);
        Assert.Contains("then_0:", nasm);
        Assert.Contains("else_0:", nasm);
        Assert.Contains("endif_0:", nasm);
    }

    [Fact]
    public void Emit_WhileLoop_GeneratesLabels()
    {
        var nasm = Emit("program test;\nbegin test;\nwhile(rax < 10) do\n    add(1, rax);\nendwhile;\nend test;");
        Assert.Contains("while_header_0:", nasm);
        Assert.Contains("cmp rax, 10", nasm);
        Assert.Contains("jge endwhile", nasm);
        Assert.Contains("while_body_0:", nasm);
        Assert.Contains("jmp while_header_0", nasm);
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
        Assert.Contains("mov rax, [rbp-8]", nasm);
        Assert.Contains("add rax, [rbp-16]", nasm);
        Assert.Contains("pop rbp", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_ProcedureWithVariables_StackLayout()
    {
        var source = "program test;\nprocedure Foo(x:int64); @returns(\"rax\");\nvar\n    total:int64;\nbegin Foo;\n    mov(x, rax);\nend Foo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = Emit(source);
        Assert.Contains("push rbp", nasm);
        Assert.Contains("mov [rbp-8], rdi", nasm);
        Assert.Contains("mov rax, [rbp-8]", nasm);
        Assert.Contains("ret", nasm);
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
        var nasm = Emit("program test;\nbegin test;\n    mov(42, rbx);\nend test;");
        Assert.Contains("xor ebx, ebx", nasm);
        Assert.Contains("mov rdi, rbx", nasm);
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
        var nasm = Emit("program t;\n#include(\"stdlib64.hhf\")\nbegin t;\n    mov(1, rax);\nend t;");
        Assert.DoesNotContain("include", nasm, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stdlib64", nasm, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("mov rax, 1", nasm);
    }

    [Fact]
    public void Emit_HelloWorldSample_GeneratesRunnableNasm()
    {
        var source = File.ReadAllText(GetSamplePath("hello", "hello.hla64"));
        var nasm = Emit(source);

        Assert.Contains("bits 64", nasm);
        Assert.Contains("section .text", nasm);
        Assert.Contains("section .data", nasm);
        Assert.Contains("global _start", nasm);
        Assert.Contains("_start:", nasm);
        Assert.Contains("syscall", nasm);

        Assert.Contains("str_0 db \"Hello from HlaX64\", 0", nasm);
        Assert.Contains("newline db 0x0A", nasm);
        Assert.Contains("lea rsi, [str_0]", nasm);

        Assert.Contains("; RUNTIME: stdout_put_str", nasm);
        Assert.Contains("; RUNTIME: stdout_put_nl", nasm);
    }

    [Fact]
    public void Emit_ExitCodeSample_GeneratesExpectedExitSyscall()
    {
        var source = File.ReadAllText(GetSamplePath("exitcode", "exitcode.hla64"));
        var nasm = Emit(source);
        Assert.Contains("mov rbx, 42", nasm);
        Assert.Contains("mov rax, 60", nasm);
        Assert.Contains("syscall", nasm);
    }

    private static string GetSamplePath(string sampleDir, string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "tests", "samples", sampleDir, fileName);
            if (File.Exists(candidate))
                return candidate;

            candidate = Path.Combine(dir.FullName, "..", "..", "..", "..", "tests", "samples", sampleDir, fileName);
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);

            dir = dir.Parent;
        }
        throw new FileNotFoundException(
            $"Could not locate sample {sampleDir}/{fileName} starting from {AppContext.BaseDirectory}");
    }

    [Fact]
    public void Emit_PointerLoadStore_EmitsLeaAndMemLoad()
    {
        var source = @"program t;
procedure P; @returns(""rax"");
var slot: int64;
begin P;
    mov(42, slot);
    mov(&slot, rcx);
    mov([rcx], rax);
end P;
begin t;
    mov(0, rax);
end t;";
        var nasm = Emit(source);
        Assert.Contains("lea rcx, [rbp-", nasm);
        Assert.Contains("mov rax, [rcx]", nasm);
    }

    [Fact]
    public void Emit_StringLength_EmitsByteLoadAndLeaString()
    {
        var source = @"program t;
procedure S; @returns(""rax"");
var ch: int64;
begin S;
    mov(&""hello"", rcx);
    mov([rcx].byte, ch);
end S;
begin t;
    mov(0, rax);
end t;";
        var nasm = Emit(source);
        Assert.Contains("lea rcx, [str_", nasm);
        Assert.Contains("movzx", nasm);
        Assert.Contains("byte [rcx]", nasm);
    }
}

public class WindowsMsAbiLowererTests
{
    private static string EmitForWindows(string source)
    {
        var options = CompilationOptions.Default with { Target = TargetTriple.WindowsX64MsAbi };
        var compilation = new Compilation("(test)", source, options);
        var result = compilation.Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
        var emitter = new NasmEmitter();
        return emitter.Emit(result.LoweredFunctions, result.StringLiterals);
    }

    [Fact]
    public void Emit_SimpleProgram_ContainsGlobalStart()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        Assert.Contains("global _start", nasm);
        Assert.Contains("_start:", nasm);
        Assert.Contains("section .text", nasm);
        Assert.Contains("section .data", nasm);
    }

    [Fact]
    public void Emit_MovInstruction_UsesCorrectMnemonic()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        Assert.Contains("mov rax, 1", nasm);
    }

    [Fact]
    public void Emit_WindowsEntryPoint_UsesExitProcess()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        Assert.Contains("extern ExitProcess", nasm);
        Assert.Contains("sub rsp, 32     ; shadow space", nasm);
        Assert.Contains("mov ecx, ebx", nasm);
        Assert.Contains("call ExitProcess", nasm);
    }

    [Fact]
    public void Emit_WindowsEntryPoint_HasNoSyscall()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    mov(1, rax);\nend test;");
        Assert.DoesNotContain("syscall", nasm);
        Assert.DoesNotContain("mov rax, 60", nasm);
    }

    [Fact]
    public void Emit_WindowsProcedure_UsesRcxRdxForArgs()
    {
        var source = "program test;\nprocedure AddTwo(a:int64; b:int64); @returns(\"rax\");\nbegin AddTwo;\n    mov(a, rax);\n    add(b, rax);\nend AddTwo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = EmitForWindows(source);
        Assert.Contains("AddTwo:", nasm);
        Assert.Contains("push rbp", nasm);
        Assert.Contains("mov rbp, rsp", nasm);
        Assert.Contains("mov [rbp-8], rcx", nasm);
        Assert.Contains("mov [rbp-16], rdx", nasm);
        Assert.Contains("mov rax, [rbp-8]", nasm);
        Assert.Contains("add rax, [rbp-16]", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_WindowsProcedure_UsesCorrectRegisterArgs()
    {
        var source = "program test;\nprocedure Foo(x:int64; y:int64); @returns(\"rax\");\nbegin Foo;\n    mov(x, rax);\nend Foo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = EmitForWindows(source);
        Assert.Contains("Foo:", nasm);
        Assert.Contains("mov [rbp-8], rcx", nasm);
        Assert.Contains("mov [rbp-16], rdx", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_WindowsStdoutPut_UsesRuntimeFunctions()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    stdout.put(\"Hello\", nl);\nend test;");
        Assert.Contains("extern stdout_put_str", nasm);
        Assert.Contains("lea rcx, [str_0]", nasm);
        Assert.Contains("call stdout_put_str", nasm);
        Assert.Contains("call stdout_put_nl", nasm);
    }

    [Fact]
    public void Emit_WindowsIfElse_GeneratesLabels()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\nif(rax = 0) then\n    mov(1, rbx);\nelse\n    mov(2, rbx);\nendif;\nend test;");
        Assert.Contains("cmp rax, 0", nasm);
        Assert.Contains("je then_0", nasm);
        Assert.Contains("jmp else_0", nasm);
    }

    [Fact]
    public void Emit_WindowsProcedureDef_UsesRcxRdxForArgs()
    {
        var source = @"program test;
procedure Sum(a:int64; b:int64); @returns(""rax"");
begin Sum;
    mov(a, rax);
    add(b, rax);
end Sum;
begin test;
    mov(1, rax);
end test;";
        var nasm = EmitForWindows(source);
        Assert.Contains("Sum:", nasm);
        Assert.Contains("mov [rbp-8], rcx", nasm);
        Assert.Contains("mov [rbp-16], rdx", nasm);
        Assert.Contains("mov rax, [rbp-8]", nasm);
        Assert.Contains("add rax, [rbp-16]", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_WindowsWhileLoop_GeneratesLabels()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\nwhile(rax < 10) do\n    add(1, rax);\nendwhile;\nend test;");
        Assert.Contains("while_header_0:", nasm);
        Assert.Contains("cmp rax, 10", nasm);
        Assert.Contains("while_body_0:", nasm);
    }

    [Fact]
    public void Emit_WindowsStdoutPutRegister_UsesLibraryMode()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    stdout.put(rax, nl);\nend test;");
        Assert.Contains("extern stdout_put_int", nasm);
        Assert.Contains("mov rcx, rax", nasm);
        Assert.Contains("call stdout_put_int", nasm);
        Assert.Contains("call stdout_put_nl", nasm);
    }
}