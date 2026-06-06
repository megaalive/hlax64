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
        Assert.Contains(".Lpop_0:", nasm);
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
        Assert.Contains("mov rdi, rax", nasm);
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
}