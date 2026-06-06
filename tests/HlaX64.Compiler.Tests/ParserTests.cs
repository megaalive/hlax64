using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Lexing;
using HlaX64.Compiler.Parsing;

namespace HlaX64.Compiler.Tests;

public class ParserTests
{
    [Fact]
    public void Parse_SimpleProgram_ReturnsProgramNode()
    {
        var source = "program simple;\nbegin simple;\n    mov(1, rax);\nend simple;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        Assert.NotNull(program);
        Assert.Equal("simple", program.Name);
        Assert.NotEmpty(program.Statements);
    }

    [Fact]
    public void Parse_MovInstruction_GeneratesCorrectNode()
    {
        var source = "program test;\nbegin test;\n    mov(1, rax);\nend test;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var instr = Assert.IsType<InstructionNode>(program.Statements[0]);
        Assert.Equal("mov", instr.Mnemonic);
        Assert.Equal(2, instr.Operands.Count);
        Assert.IsType<IntegerLiteralNode>(instr.Operands[0]);
        Assert.IsType<RegisterNode>(instr.Operands[1]);

        var intLit = (IntegerLiteralNode)instr.Operands[0];
        Assert.Equal(1, intLit.Value);

        var reg = (RegisterNode)instr.Operands[1];
        Assert.Equal("rax", reg.Name);
    }

    [Fact]
    public void Parse_AddInstruction_OperandOrderPreserved()
    {
        var source = "program test;\nbegin test;\n    add(2, rax);\nend test;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var instr = Assert.IsType<InstructionNode>(program.Statements[0]);
        Assert.Equal("add", instr.Mnemonic);
    }

    [Fact]
    public void Parse_ProgramWithInclude_ParsesIncludeNode()
    {
        var source = "program hello;\n#include(\"stdlib64.hhf\")\nbegin hello;\nend hello;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        Assert.Single(program.Includes);
        var include = Assert.IsType<IncludeNode>(program.Includes[0]);
        Assert.Equal("stdlib64.hhf", include.Path);
    }

    [Fact]
    public void Parse_MultipleInstructions_ParsesAll()
    {
        var source = "program test;\nbegin test;\n    mov(1, rax);\n    add(2, rax);\n    xor(rcx, rcx);\nend test;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        Assert.Equal(3, program.Statements.Count);
        Assert.IsType<InstructionNode>(program.Statements[0]);
        Assert.IsType<InstructionNode>(program.Statements[1]);
        Assert.IsType<InstructionNode>(program.Statements[2]);
    }

    [Fact]
    public void Parse_ProcedureWithParams_ParsesCorrectly()
    {
        var source = "program main;\n\nprocedure AddTwo(a:int64; b:int64); @returns(\"rax\");\nbegin AddTwo;\n    mov(a, rax);\n    add(b, rax);\nend AddTwo;\n\nbegin main;\nend main;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        Assert.Equal("AddTwo", proc.Name);
        Assert.Equal(2, proc.Parameters.Count);
        Assert.Equal("a", proc.Parameters[0].Name);
        Assert.Equal("int64", proc.Parameters[0].Type);
        Assert.Equal("b", proc.Parameters[1].Name);
        Assert.Equal("int64", proc.Parameters[1].Type);
        Assert.Equal("rax", proc.ReturnsRegister);
    }

    [Fact]
    public void Parse_IfStatement_ParsesCorrectly()
    {
        var source = "program test;\nbegin test;\n    if(rax = 0) then\n        mov(1, rbx);\n    else\n        mov(2, rbx);\n    endif;\nend test;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var ifNode = Assert.IsType<IfNode>(program.Statements[0]);
        Assert.IsType<ComparisonNode>(ifNode.Condition);
        Assert.Single(ifNode.ThenBody);
        Assert.Single(ifNode.ElseBody);
    }

    [Fact]
    public void Parse_WhileStatement_ParsesCorrectly()
    {
        var source = "program test;\nbegin test;\n    while(rcx < rdx) do\n        add(1, rcx);\n    endwhile;\nend test;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var whileNode = Assert.IsType<WhileNode>(program.Statements[0]);
        Assert.IsType<ComparisonNode>(whileNode.Condition);
        Assert.Single(whileNode.Body);
    }

    [Fact]
    public void Parse_StdoutPut_ParsesAsCallNode()
    {
        var source = "program hello;\nbegin hello;\n    stdout.put(\"Hello\", nl);\nend hello;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var call = Assert.IsType<CallNode>(program.Statements[0]);
        Assert.Equal("stdout.put", call.Name);
        Assert.Equal(2, call.Arguments.Count);
    }

    [Fact]
    public void Parse_InvalidProgram_ThrowsParseException()
    {
        var source = "begin nomatch;\nend nomatch;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);

        Assert.Throws<ParseException>(() => parser.Parse());
    }

    [Fact]
    public void Parse_LocalVariables_ParsesCorrectly()
    {
        var source = "program main;\n\nprocedure SumTo(n:int64); @returns(\"rax\");\nvar\n    total:int64;\n    i:int64;\nbegin SumTo;\n    mov(0, total);\n    mov(0, i);\nend SumTo;\n\nbegin main;\nend main;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        var proc = Assert.IsType<ProcedureNode>(program.Statements[0]);
        Assert.NotEmpty(proc.Variables);
        Assert.Equal("total", ((VariableNode)proc.Variables[0]).Name);
        Assert.Equal("i", ((VariableNode)proc.Variables[1]).Name);
    }

    [Fact]
    public void Parse_CallProcName_ParsesAsCallNode()
    {
        // `call AddTwo(10, 20);` is the procedure-call syntax documented
        // in docs/abi-linux-x64.md. The "call" word is a soft keyword.
        var source = "program main;\n\nprocedure AddTwo(a:int64; b:int64); @returns(\"rax\");\nbegin AddTwo;\nend AddTwo;\n\nbegin main;\n    call AddTwo(10, 20);\nend main;";
        var lexer = new Lexer(source);
        var tokens = lexer.Tokenize();
        var parser = new Parser(tokens);
        var program = parser.Parse();

        // The body of `begin main;` is program.Statements[1] (after the proc).
        var call = Assert.IsType<CallNode>(program.Statements[1]);
        Assert.Equal("AddTwo", call.Name);
        Assert.Equal(2, call.Arguments.Count);
        Assert.IsType<IntegerLiteralNode>(call.Arguments[0]);
        Assert.IsType<IntegerLiteralNode>(call.Arguments[1]);
        Assert.Equal(10, ((IntegerLiteralNode)call.Arguments[0]).Value);
        Assert.Equal(20, ((IntegerLiteralNode)call.Arguments[1]).Value);
    }
}
