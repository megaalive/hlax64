using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Ir;

public sealed class AstToIrLowering
{
    private readonly Dictionary<string, IntegerTypeSymbol> _regTypes = new()
    {
        ["al"] = TypeRegistry.Int8, ["bl"] = TypeRegistry.Int8,
        ["cl"] = TypeRegistry.Int8, ["dl"] = TypeRegistry.Int8,
        ["ax"] = TypeRegistry.Int16, ["bx"] = TypeRegistry.Int16,
        ["cx"] = TypeRegistry.Int16, ["dx"] = TypeRegistry.Int16,
        ["eax"] = TypeRegistry.Int32, ["ebx"] = TypeRegistry.Int32,
        ["ecx"] = TypeRegistry.Int32, ["edx"] = TypeRegistry.Int32,
        ["esi"] = TypeRegistry.Int32, ["edi"] = TypeRegistry.Int32,
        ["r8d"] = TypeRegistry.Int32, ["r9d"] = TypeRegistry.Int32,
        ["r10d"] = TypeRegistry.Int32, ["r11d"] = TypeRegistry.Int32,
        ["r12d"] = TypeRegistry.Int32, ["r13d"] = TypeRegistry.Int32,
        ["r14d"] = TypeRegistry.Int32, ["r15d"] = TypeRegistry.Int32,
        ["rax"] = TypeRegistry.Int64, ["rbx"] = TypeRegistry.Int64,
        ["rcx"] = TypeRegistry.Int64, ["rdx"] = TypeRegistry.Int64,
        ["rsi"] = TypeRegistry.Int64, ["rdi"] = TypeRegistry.Int64,
        ["rbp"] = TypeRegistry.Int64, ["rsp"] = TypeRegistry.Int64,
        ["r8"] = TypeRegistry.Int64, ["r9"] = TypeRegistry.Int64,
        ["r10"] = TypeRegistry.Int64, ["r11"] = TypeRegistry.Int64,
        ["r12"] = TypeRegistry.Int64, ["r13"] = TypeRegistry.Int64,
        ["r14"] = TypeRegistry.Int64, ["r15"] = TypeRegistry.Int64,
    };

    public IrFunction LowerProgram(ProgramNode program)
    {
        var func = new IrFunction("_start");

        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
                LowerProcedure(proc);
            else
                LowerStatement(stmt, func.EntryBlock);
        }

        return func;
    }

    public IrFunction LowerProcedure(ProcedureNode proc)
    {
        var func = new IrFunction(proc.Name);
        var block = func.EntryBlock;

        foreach (var stmt in proc.Body)
            LowerStatement(stmt, block);

        return func;
    }

    private void LowerStatement(AstNode node, IrBasicBlock block)
    {
        switch (node)
        {
            case InstructionNode instr:
                LowerInstruction(instr, block);
                break;
            case CallNode call:
                LowerCall(call, block);
                break;
            case IfNode ifNode:
                LowerIf(ifNode, block);
                break;
            case WhileNode whileNode:
                LowerWhile(whileNode, block);
                break;
        }
    }

    private void LowerInstruction(InstructionNode instr, IrBasicBlock block)
    {
        var mnemonic = instr.Mnemonic.ToLowerInvariant();

        if (instr.Operands.Count == 2)
        {
            var dst = ResolveOrCreateValue(instr.Operands[1]);
            var src = ResolveOrCreateValue(instr.Operands[0]);

            var opcode = mnemonic switch
            {
                "mov" => IrOpcode.Move,
                "add" => IrOpcode.Add,
                "sub" => IrOpcode.Subtract,
                "imul" => IrOpcode.Multiply,
                "xor" => IrOpcode.Move,
                "cmp" => IrOpcode.Compare,
                "and" => IrOpcode.Move,
                "or" => IrOpcode.Move,
                _ => IrOpcode.Move
            };

            if (opcode == IrOpcode.Compare)
            {
                block.Add(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { dst, src }));
            }
            else if (opcode == IrOpcode.Move && mnemonic == "xor" && instr.Operands[0] is RegisterNode r1
                     && instr.Operands[1] is RegisterNode r2 && r1.Name == r2.Name)
            {
                block.Add(new IrInstruction(IrOpcode.LoadConstant, dst, immediate: 0L));
            }
            else
            {
                var result = new IrValue(dst.Type);
                block.Add(new IrInstruction(opcode, result, new List<IrValue> { src, dst }));
                block.Add(new IrInstruction(IrOpcode.Move, dst, new List<IrValue> { result }));
            }
        }
        else if (instr.Operands.Count == 1)
        {
            var op = ResolveOrCreateValue(instr.Operands[0]);
            block.Add(new IrInstruction(IrOpcode.Move, op, new List<IrValue> { op }));
        }
        else
        {
            block.Add(new IrInstruction(IrOpcode.Move));
        }
    }

    private void LowerCall(CallNode call, IrBasicBlock block)
    {
        if (call.Name == "stdout.put")
        {
            foreach (var arg in call.Arguments)
            {
                if (arg is RegisterNode || arg is IntegerLiteralNode)
                {
                    var val = ResolveOrCreateValue(arg);
                    block.Add(new IrInstruction(IrOpcode.Call, operands: new List<IrValue> { val }, immediate: "stdout.put"));
                }
            }
            return;
        }

        foreach (var arg in call.Arguments)
            ResolveOrCreateValue(arg);

        block.Add(new IrInstruction(IrOpcode.Call, immediate: call.Name));
    }

    private void LowerIf(IfNode ifNode, IrBasicBlock block)
    {
        if (ifNode.Condition is ComparisonNode comp)
        {
            var left = ResolveOrCreateValue(comp.Left);
            var right = ResolveOrCreateValue(comp.Right);

            var cmpKind = comp.Operator switch
            {
                "=" => CompareKind.Equal,
                "<" => CompareKind.LessThanSigned,
                ">" => CompareKind.GreaterThanSigned,
                _ => CompareKind.Equal
            };

            block.Add(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { left, right })
            {
                CmpKind = cmpKind
            });

            var thenBlock = new IrBasicBlock("then");
            var elseBlock = ifNode.ElseBody.Count > 0 ? new IrBasicBlock("else") : null;
            var endBlock = new IrBasicBlock("endif");

            block.Add(new IrInstruction(IrOpcode.ConditionalBranch)
            {
                TargetBlock = thenBlock.Label,
                CmpKind = cmpKind
            });

            block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = (elseBlock ?? endBlock).Label });

            foreach (var stmt in ifNode.ThenBody)
                LowerStatement(stmt, thenBlock);
            thenBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = endBlock.Label });

            if (elseBlock != null)
            {
                foreach (var stmt in ifNode.ElseBody)
                    LowerStatement(stmt, elseBlock);
                elseBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = endBlock.Label });
            }
        }
    }

    private void LowerWhile(WhileNode whileNode, IrBasicBlock block)
    {
        var headerBlock = new IrBasicBlock("while_header");
        var bodyBlock = new IrBasicBlock("while_body");
        var endBlock = new IrBasicBlock("endwhile");

        block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = headerBlock.Label });

        if (whileNode.Condition is ComparisonNode comp)
        {
            var left = ResolveOrCreateValue(comp.Left);
            var right = ResolveOrCreateValue(comp.Right);

            var cmpKind = comp.Operator switch
            {
                "=" => CompareKind.Equal,
                "<" => CompareKind.LessThanSigned,
                ">" => CompareKind.GreaterThanSigned,
                _ => CompareKind.Equal
            };

            headerBlock.Add(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { left, right })
            {
                CmpKind = cmpKind
            });

            var exitKind = comp.Operator switch
            {
                "=" => CompareKind.NotEqual,
                "<" => CompareKind.GreaterOrEqualSigned,
                ">" => CompareKind.LessOrEqualSigned,
                _ => CompareKind.NotEqual
            };

            headerBlock.Add(new IrInstruction(IrOpcode.ConditionalBranch)
            {
                TargetBlock = endBlock.Label,
                CmpKind = exitKind
            });
        }

        headerBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = bodyBlock.Label });

        foreach (var stmt in whileNode.Body)
            LowerStatement(stmt, bodyBlock);
        bodyBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = headerBlock.Label });
    }

    private IrValue ResolveOrCreateValue(AstNode node)
    {
        return node switch
        {
            RegisterNode reg => GetOrCreateRegisterValue(reg.Name),
            IntegerLiteralNode lit => new IrValue(null) /* type unknown */,
            _ => new IrValue(null)
        };
    }

    private readonly Dictionary<string, IrValue> _registerValues = new();

    private IrValue GetOrCreateRegisterValue(string name)
    {
        var key = name.ToLowerInvariant();
        if (!_registerValues.TryGetValue(key, out var value))
        {
            _regTypes.TryGetValue(key, out var type);
            value = new IrValue(type);
            _registerValues[key] = value;
        }
        return value;
    }
}
