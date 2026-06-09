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
    public void Emit_SharedLibrary_OmitsStartAndExitProcess()
    {
        const string source = """
            program lib;
            export procedure Add(a:int64; b:int64); @returns("rax");
            begin Add;
                mov(a, rax);
                add(b, rax);
            end Add;
            begin lib;
            end lib;
            """;

        var compilation = new Compilation("(test)", source);
        var result = compilation.Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));

        var emitter = new NasmEmitter();
        var nasm = emitter.Emit(
            result.LoweredFunctions,
            result.StringLiterals,
            result.GlobalData,
            new NasmEmitOptions { IsSharedLibrary = true });

        Assert.Contains("global Add", nasm);
        Assert.DoesNotContain("global _start", nasm);
        Assert.DoesNotContain("_start:", nasm);
        Assert.DoesNotContain("ExitProcess", nasm);
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
        Assert.Contains("mov qword [rbp-8], rdi", nasm);
        Assert.Contains("mov qword [rbp-16], rsi", nasm);
        Assert.Contains("mov rax, qword [rbp-8]", nasm);
        Assert.Contains("add rax, qword [rbp-16]", nasm);
        Assert.Contains("pop rbp", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_ProcedureWithVariables_StackLayout()
    {
        var source = "program test;\nprocedure Foo(x:int64); @returns(\"rax\");\nvar\n    total:int64;\nbegin Foo;\n    mov(x, rax);\nend Foo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = Emit(source);
        Assert.Contains("push rbp", nasm);
        Assert.Contains("mov qword [rbp-8], rdi", nasm);
        Assert.Contains("mov rax, qword [rbp-8]", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_ProcedureWithReturnAndVariables()
    {
        var source = "program test;\nprocedure Sum(a:int64; b:int64); @returns(\"rax\");\nvar\n    result:int64;\nbegin Sum;\n    mov(a, rax);\n    add(b, rax);\nend Sum;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = Emit(source);
        Assert.Contains("Sum:", nasm);
        Assert.Contains("mov qword [rbp-8], rdi", nasm);
        Assert.Contains("mov qword [rbp-16], rsi", nasm);
        Assert.Contains("mov rax, qword [rbp-8]", nasm);
        Assert.Contains("add rax, qword [rbp-16]", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_ProcedureEightArgs_LoadsStackParamsFromCallerFrame()
    {
        var source = @"program test;
procedure Sum8(a:int64; b:int64; c:int64; d:int64; e:int64; f:int64; g:int64; h:int64); @returns(""rax"");
begin Sum8;
    mov(a, rax);
    add(h, rax);
end Sum8;
begin test;
    call Sum8(1, 2, 3, 4, 5, 6, 7, 8);
end test;";
        var nasm = Emit(source);
        Assert.Contains("mov qword [rbp-48], r9", nasm);
        Assert.Contains("mov rax, qword [rbp+16]", nasm);
        Assert.Contains("mov qword [rbp-56], rax", nasm);
        Assert.Contains("mov rax, qword [rbp+24]", nasm);
        Assert.Contains("mov qword [rbp-64], rax", nasm);
    }

    [Fact]
    public void Emit_CallEightArgs_PushesStackArgsRightToLeft()
    {
        var source = @"program test;
procedure Sum8(a:int64; b:int64; c:int64; d:int64; e:int64; f:int64; g:int64; h:int64); @returns(""rax"");
begin Sum8;
    mov(a, rax);
end Sum8;
begin test;
    call Sum8(1, 2, 3, 4, 5, 6, 7, 8);
end test;";
        var nasm = Emit(source);
        Assert.Contains("push 8", nasm);
        Assert.Contains("push 7", nasm);
        Assert.Contains("mov rdi, 1", nasm);
        Assert.Contains("mov r9, 6", nasm);
        Assert.Contains("call Sum8", nasm);
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
        Assert.Contains("mov rax, qword [rcx]", nasm);
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
        Assert.Contains("lea rcx, [rel str_", nasm);
        Assert.Contains("movzx", nasm);
        Assert.Contains("byte [rcx]", nasm);
    }

    [Fact]
    public void Emit_IdivJmpAndLabel_EmitsInlineAsm()
    {
        const string source = """
            program test;
            procedure P; @returns("rax");
            begin P;
                mov(20, rax);
                idiv(4, rax);
                jmp(skip);
                mov(99, rax);
            skip:
                mov(rax, rcx);
            end P;
            begin test;
            end test;
            """;
        var nasm = Emit(source);
        Assert.Contains("idiv", nasm);
        Assert.Contains("jmp skip", nasm);
        Assert.Contains("skip:", nasm);
    }

    [Fact]
    public void Emit_StructAlias_ParsesRecordLayout()
    {
        const string source = """
            program test;
            struct Pair
                a: int64;
                b: int64;
            endstruct;
            begin test;
                mov(0, rax);
            end test;
            """;
        var result = new Compilation("(test)", source).Process();
        Assert.True(result.Success, string.Join("; ", result.Diagnostics));
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
        Assert.Contains("sub rsp, 40     ; shadow + align", nasm);
        Assert.Contains("add rsp, 40     ; restore entry shadow + align", nasm);
        Assert.Contains("mov rcx, rbx", nasm);
        Assert.Contains("jmp ExitProcess", nasm);
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
        Assert.Contains("mov qword [rbp-8], rcx", nasm);
        Assert.Contains("mov qword [rbp-16], rdx", nasm);
        Assert.Contains("mov rax, qword [rbp-8]", nasm);
        Assert.Contains("add rax, qword [rbp-16]", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_WindowsProcedure_UsesCorrectRegisterArgs()
    {
        var source = "program test;\nprocedure Foo(x:int64; y:int64); @returns(\"rax\");\nbegin Foo;\n    mov(x, rax);\nend Foo;\nbegin test;\n    mov(1, rax);\nend test;";
        var nasm = EmitForWindows(source);
        Assert.Contains("Foo:", nasm);
        Assert.Contains("mov qword [rbp-8], rcx", nasm);
        Assert.Contains("mov qword [rbp-16], rdx", nasm);
        Assert.Contains("ret", nasm);
    }

    [Fact]
    public void Emit_WindowsStdoutPutu_UsesRuntimeFunctions()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    stdout.putu(r11, nl);\nend test;");
        Assert.Contains("extern stdout_put_uint", nasm);
        Assert.Contains("call stdout_put_uint", nasm);
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
        Assert.Contains("mov qword [rbp-8], rcx", nasm);
        Assert.Contains("mov qword [rbp-16], rdx", nasm);
        Assert.Contains("mov rax, qword [rbp-8]", nasm);
        Assert.Contains("add rax, qword [rbp-16]", nasm);
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
        Assert.Contains("sub rsp,", nasm);
        Assert.Contains("mov [rsp+0], rax", nasm);
        Assert.Contains("mov rcx, [rsp+0]", nasm);
        Assert.Contains("call stdout_put_int", nasm);
        Assert.Contains("call stdout_put_nl", nasm);
    }

    [Fact]
    public void Emit_WindowsShiftInstruction_LowersImmediateCount()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    mov(1, r10);\n    shl(32, r10);\nend test;");
        Assert.Contains("shl r10, 32", nasm);
        Assert.DoesNotContain("mov r10, 32", nasm);
    }

    [Fact]
    public void Emit_WindowsDwordMemoryLoad_UsesThirtyTwoBitRegisterAlias()
    {
        var nasm = EmitForWindows("program test;\nbegin test;\n    mov([rcx + 32].dword, r8);\nend test;");
        Assert.Contains("mov r8d, dword [rcx+32]", nasm);
        Assert.DoesNotContain("mov r8, dword", nasm);
    }

    [Fact]
    public void Emit_WindowsProcedureFiveArgs_LoadsStackParamFromCallerFrame()
    {
        var source = @"program test;
procedure Sum5(a:int64; b:int64; c:int64; d:int64; e:int64); @returns(""rax"");
begin Sum5;
    mov(a, rax);
    add(e, rax);
end Sum5;
begin test;
    call Sum5(1, 2, 3, 4, 5);
end test;";
        var nasm = EmitForWindows(source);
        Assert.Contains("mov qword [rbp-32], r9", nasm);
        Assert.Contains("mov rax, qword [rbp+48]", nasm);
        Assert.Contains("mov qword [rbp-40], rax", nasm);
    }

    [Fact]
    public void Emit_WindowsCallFiveArgs_UsesShadowSpaceAndStackSlot()
    {
        var source = @"program test;
procedure Sum5(a:int64; b:int64; c:int64; d:int64; e:int64); @returns(""rax"");
begin Sum5;
    mov(a, rax);
end Sum5;
begin test;
    call Sum5(1, 2, 3, 4, 5);
end test;";
        var nasm = EmitForWindows(source);
        // 1 stack arg => 8 bytes, padded to 16 to keep RSP 16-byte aligned
        // at the call (32-byte shadow + 16 = 48).
        Assert.Contains("sub rsp, 48", nasm);
        Assert.Contains("qword [0 + rsp + 32], 5", nasm);
        Assert.Contains("mov rcx, 1", nasm);
        Assert.Contains("call Sum5", nasm);
    }

    [Fact]
    public void Emit_WindowsCallSevenArgs_AllocatesSixteenByteAlignedStack()
    {
        // CreateFileA-style call: 4 register args + 3 stack args. The stack
        // allocation must stay a multiple of 16 so RSP is 16-byte aligned at
        // the CALL; an odd stack-arg count previously produced `sub rsp, 56`
        // which crashed callees that use aligned SSE stores.
        var source = @"program test;
procedure Sum7(a:int64; b:int64; c:int64; d:int64; e:int64; f:int64; g:int64); @returns(""rax"");
begin Sum7;
    mov(a, rax);
end Sum7;
begin test;
    call Sum7(1, 2, 3, 4, 5, 6, 7);
end test;";
        var nasm = EmitForWindows(source);
        // 3 stack args => 24 bytes, padded to 32 (32-byte shadow + 32 = 64).
        Assert.Contains("sub rsp, 64", nasm);
        Assert.DoesNotContain("sub rsp, 56", nasm);
        Assert.Contains("qword [0 + rsp + 32], 5", nasm);
        Assert.Contains("qword [8 + rsp + 32], 6", nasm);
        Assert.Contains("qword [16 + rsp + 32], 7", nasm);
        Assert.Contains("call Sum7", nasm);
    }

    [Fact]
    public void Emit_WindowsXorAndAndOr_EmitBitwiseOpsNotMove()
    {
        var source = @"program test;
begin test;
    mov(15, r11);
    mov(255, r12);
    xor(9, r11);
    and(15, r12);
    or(3, r12);
end test;";
        var nasm = EmitForWindows(source);
        Assert.Contains("xor r11, 9", nasm);
        Assert.Contains("and r12, 15", nasm);
        Assert.Contains("or r12, 3", nasm);
        Assert.DoesNotContain("mov r11, 9", nasm);
        Assert.DoesNotContain("mov r12, 15", nasm);
    }

    [Fact]
    public void Emit_WindowsWhileWithNestedIf_DoesNotFallThroughLoopContinuation()
    {
        // Regression: when a while loop body contains an if, the loop's
        // continuation block (which carries the procedure tail/epilogue) must
        // be laid out AFTER the if's blocks. Otherwise the continuation falls
        // through into the then-block and jumps back into the loop forever.
        var source = @"program test;
procedure CountNewlines(n:int64); @returns(""rax"");
begin CountNewlines;
    mov(0, r11);
    mov(0, r8);
    while(r8 < n) do
        mov([r8].byte, r9);
        if(r9 = 10) then
            add(1, r11);
        endif;
        add(1, r8);
    endwhile;
    mov(r11, rax);
end CountNewlines;
begin test;
    mov(1, rax);
end test;";
        var nasm = EmitForWindows(source);
        var lines = nasm.Replace("\r", "").Split('\n');

        int contIdx = Array.FindIndex(lines, l => l.Trim() == "cont_0:");
        int thenIdx = Array.FindIndex(lines, l => l.Trim() == "then_1:");
        Assert.True(contIdx >= 0, "expected loop continuation block cont_0");
        Assert.True(thenIdx >= 0, "expected nested if then_1 block");

        // The nested if blocks must precede the loop continuation block.
        Assert.True(thenIdx < contIdx,
            $"then_1 (idx {thenIdx}) must come before cont_0 (idx {contIdx}) so the loop tail does not fall into the if body");

        // The continuation block must terminate with the procedure epilogue
        // (ret), not fall through to another block.
        var tail = string.Join("\n", lines[contIdx..]);
        Assert.Contains("ret", tail);
    }
}