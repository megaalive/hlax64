using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Ir;

public sealed class AstToIrLowering
{
    public IrFunction LowerProgram(ProgramNode program, List<IrFunction> procedures)
    {
        var func = new IrFunction("_start");
        procedures.Clear();

        var currentBlock = func.EntryBlock;
        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
            {
                var procIr = LowerProcedure(proc);
                procedures.Add(procIr);
            }
            else
            {
                currentBlock = LowerStatement(stmt, currentBlock, func);
            }
        }

        func.EnsureBlocksRegistered();
        return func;
    }

    public IrFunction LowerProcedure(ProcedureNode proc)
    {
        var func = new IrFunction(proc.Name);
        func.IsExport = proc.IsExport;

        foreach (var param in proc.Parameters)
        {
            func.ParameterValues.Add(new IrValue { Name = param.Name });
        }

        foreach (var variable in proc.Variables)
        {
            if (variable is VariableNode varNode)
            {
                func.LocalValues.Add(new IrValue { Name = varNode.Name });
                var elemSize = (TypeRegistry.Lookup(varNode.Type)?.BitWidth ?? 64) / 8;
                func.LocalLayouts[varNode.Name] = new IrLocalLayout
                {
                    ElementCount = varNode.ElementCount,
                    ElementSizeBytes = elemSize
                };
            }
        }

        var currentBlock = func.EntryBlock;
        foreach (var stmt in proc.Body)
            currentBlock = LowerStatement(stmt, currentBlock, func);

        func.EnsureBlocksRegistered();
        return func;
    }

    private IrBasicBlock LowerStatement(AstNode node, IrBasicBlock block, IrFunction func)
    {
        switch (node)
        {
            case InstructionNode instr:
                LowerInstruction(instr, block);
                return block;
            case CallNode call:
                LowerCall(call, block);
                return block;
            case IfNode ifNode:
                return LowerIf(ifNode, block, func);
            case WhileNode whileNode:
                return LowerWhile(whileNode, block, func);
        }
        return block;
    }

    private void LowerInstruction(InstructionNode instr, IrBasicBlock block)
    {
        var mnemonic = instr.Mnemonic.ToLowerInvariant();

        if (instr.Operands.Count == 2)
        {
            var dst = ResolveOperand(instr.Operands[1]);
            var src = ResolveOperand(instr.Operands[0]);

            if (mnemonic == "xor" && IsSameReg(instr.Operands[0], instr.Operands[1]))
            {
                block.Add(new IrInstruction(IrOpcode.LoadConstant, dst, immediate: 0L));
                return;
            }

            var opcode = mnemonic switch
            {
                "mov" => IrOpcode.Move,
                "add" => IrOpcode.Add,
                "sub" => IrOpcode.Subtract,
                "imul" => IrOpcode.Multiply,
                "xor" => IrOpcode.Move,
                "and" => IrOpcode.Move,
                "or" => IrOpcode.Move,
                "cmp" => IrOpcode.Compare,
                _ => IrOpcode.Move
            };

            if (opcode == IrOpcode.Compare)
            {
                block.Add(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { dst, src }));
            }
            else
            {
                block.Add(new IrInstruction(opcode, dst, new List<IrValue> { src }));
            }
        }
        else if (instr.Operands.Count == 1)
        {
            var op = ResolveOperand(instr.Operands[0]);
            if (mnemonic is "inc" or "dec")
            {
                var opcode = mnemonic == "inc" ? IrOpcode.Add : IrOpcode.Subtract;
                block.Add(new IrInstruction(opcode, op, new List<IrValue> { new() { Name = "imm:1" } }));
            }
            else
            {
                block.Add(new IrInstruction(IrOpcode.Move, op, new List<IrValue> { op }));
            }
        }
    }

    private static bool IsSameReg(AstNode a, AstNode b)
    {
        return a is RegisterNode ra && b is RegisterNode rb &&
               string.Equals(ra.Name, rb.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void LowerCall(CallNode call, IrBasicBlock block)
    {
        if (call.Name == "stdout.put")
        {
            var allArgs = new List<IrValue>();
            foreach (var arg in call.Arguments)
                allArgs.Add(ResolveOperand(arg));
            block.Add(new IrInstruction(IrOpcode.Call, operands: allArgs, immediate: "stdout.put"));
            return;
        }

        var callArgs = new List<IrValue>();
        foreach (var arg in call.Arguments)
            callArgs.Add(ResolveOperand(arg));

        block.Add(new IrInstruction(IrOpcode.Call, operands: callArgs, immediate: call.Name));
    }

    private int _labelCounter;

    private IrBasicBlock LowerIf(IfNode ifNode, IrBasicBlock block, IrFunction func)
    {
        if (ifNode.Condition is not ComparisonNode comp)
            return block;

        var left = ResolveOperand(comp.Left);
        var right = ResolveOperand(comp.Right);

        var cmpKind = comp.Operator switch
        {
            "=" => CompareKind.Equal,
            "<" => CompareKind.LessThanSigned,
            ">" => CompareKind.GreaterThanSigned,
            "<?" => CompareKind.LessThanUnsigned,
            ">?" => CompareKind.GreaterThanUnsigned,
            _ => CompareKind.Equal
        };

        block.Add(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { left, right })
        {
            CmpKind = cmpKind
        });

        var id = _labelCounter++;
        var thenBlock = new IrBasicBlock($"then_{id}");
        var elseBlock = ifNode.ElseBody.Count > 0 ? new IrBasicBlock($"else_{id}") : null;
        var endBlock = new IrBasicBlock($"endif_{id}");
        var contBlock = new IrBasicBlock($"cont_{id}");

        func.AddBlock(thenBlock);
        if (elseBlock != null) func.AddBlock(elseBlock);
        func.AddBlock(endBlock);
        func.AddBlock(contBlock);

        block.Add(new IrInstruction(IrOpcode.ConditionalBranch)
        {
            TargetBlock = thenBlock.Label,
            CmpKind = cmpKind
        });

        block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = (elseBlock ?? contBlock).Label });

        foreach (var stmt in ifNode.ThenBody)
            LowerStatement(stmt, thenBlock, func);
        thenBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = endBlock.Label });

        if (elseBlock != null)
        {
            foreach (var stmt in ifNode.ElseBody)
                LowerStatement(stmt, elseBlock, func);
            elseBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = endBlock.Label });
        }

        endBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = contBlock.Label });

        return contBlock;
    }

    private IrBasicBlock LowerWhile(WhileNode whileNode, IrBasicBlock block, IrFunction func)
    {
        var id = _labelCounter++;
        var headerBlock = new IrBasicBlock($"while_header_{id}");
        var bodyBlock = new IrBasicBlock($"while_body_{id}");
        var endBlock = new IrBasicBlock($"endwhile_{id}");
        var contBlock = new IrBasicBlock($"cont_{id}");

        func.AddBlock(headerBlock);
        func.AddBlock(bodyBlock);
        func.AddBlock(endBlock);
        func.AddBlock(contBlock);

        block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = headerBlock.Label });

        if (whileNode.Condition is ComparisonNode comp)
        {
            var left = ResolveOperand(comp.Left);
            var right = ResolveOperand(comp.Right);

            var cmpKind = comp.Operator switch
            {
                "=" => CompareKind.Equal,
                "<" => CompareKind.LessThanSigned,
                ">" => CompareKind.GreaterThanSigned,
                "<?" => CompareKind.LessThanUnsigned,
                ">?" => CompareKind.GreaterThanUnsigned,
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
                "<?" => CompareKind.GreaterOrEqualUnsigned,
                ">?" => CompareKind.LessOrEqualUnsigned,
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
            LowerStatement(stmt, bodyBlock, func);
        bodyBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = headerBlock.Label });

        endBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = contBlock.Label });

        return contBlock;
    }

    private IrValue ResolveOperand(AstNode node)
    {
        return node switch
        {
            RegisterNode reg => GetOrCreateValue(reg.Name),
            IntegerLiteralNode lit => new IrValue { Name = $"imm:{lit.Value}" },
            StringLiteralNode str => new IrValue { Name = $"str:{str.Value}" },
            IdentifierNode ident => GetOrCreateValue(ident.Name),
            AddressOfNode addr => new IrValue { Name = $"addr:{addr.VariableName}" },
            AddressOfStringNode addrStr => new IrValue { Name = AddressRefEncoding.EncodeString(addrStr.Value) },
            MemoryRefNode mem => new IrValue
            {
                Name = MemoryRefEncoding.Encode(mem.Register, mem.Offset, mem.SizeBits)
            },
            ArrayIndexNode arr => new IrValue { Name = ArrayIndexEncoding.Encode(arr.ArrayName, arr.Index) },
            _ => new IrValue()
        };
    }

    private readonly Dictionary<string, IrValue> _values = new(StringComparer.OrdinalIgnoreCase);

    private IrValue GetOrCreateValue(string name)
    {
        if (!_values.TryGetValue(name, out var value))
        {
            value = new IrValue { Name = name };
            _values[name] = value;
        }
        return value;
    }
}