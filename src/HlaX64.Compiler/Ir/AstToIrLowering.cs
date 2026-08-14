using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Builtins;
using HlaX64.Compiler.Semantic;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Ir;

public sealed class AstToIrLowering
{
    private readonly CompileTimeConstTable _programConstTable;
    private readonly RecordTypeRegistry _recordRegistry;
    private readonly GlobalDataRegistry _globalDataRegistry;
    private readonly ExternProcedureRegistry _externRegistry;
    private readonly ProcedureTypeRegistry _procedureTypeRegistry;
    private readonly HashSet<string> _functionPointerNames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _programProcedureNames = new(StringComparer.OrdinalIgnoreCase);
    private CompileTimeConstTable _activeConstTable;
    private Dictionary<string, RecordTypeSymbol> _recordLocals = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, RecordTypeSymbol> _procedureRecordTypes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stack<(string EndLabel, string ContinueLabel)> _loopStack = new();

    public AstToIrLowering(CompileTimeConstTable? constTable = null, RecordTypeRegistry? recordRegistry = null,
        GlobalDataRegistry? globalDataRegistry = null, ExternProcedureRegistry? externRegistry = null,
        ProcedureTypeRegistry? procedureTypeRegistry = null)
    {
        _programConstTable = constTable ?? new CompileTimeConstTable();
        _recordRegistry = recordRegistry ?? new RecordTypeRegistry();
        _globalDataRegistry = globalDataRegistry ?? new GlobalDataRegistry();
        _externRegistry = externRegistry ?? new ExternProcedureRegistry();
        _procedureTypeRegistry = procedureTypeRegistry ?? new ProcedureTypeRegistry();
        _activeConstTable = _programConstTable;
    }

    public IrFunction LowerProgram(ProgramNode program, List<IrFunction> procedures)
    {
        var func = new IrFunction("_start");
        procedures.Clear();

        var currentBlock = func.EntryBlock;
        foreach (var stmt in program.Statements)
        {
            if (stmt is ProcedureNode proc)
            {
                _programProcedureNames.Add(proc.Name);
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
        _activeConstTable = BuildProcedureConstTable(proc);
        _recordLocals = new Dictionary<string, RecordTypeSymbol>(StringComparer.OrdinalIgnoreCase);
        _procedureRecordTypes = new Dictionary<string, RecordTypeSymbol>(StringComparer.OrdinalIgnoreCase);
        _functionPointerNames.Clear();
        foreach (var node in proc.Records)
        {
            if (node is RecordBlockNode block)
                _recordRegistry.Register(block, out var record, out _, _procedureRecordTypes);
        }
        var func = new IrFunction(proc.Name);
        func.IsExport = proc.IsExport;
        func.ReturnsRegister = proc.ReturnsRegister;

        foreach (var param in proc.Parameters)
        {
            func.ParameterValues.Add(new IrValue { Name = param.Name });
            func.ParameterTypes.Add((param.Name, param.Type));
            if (_procedureTypeRegistry.Contains(param.Type))
                _functionPointerNames.Add(param.Name);
            else if (TryResolveRecordType(param.Type, out var recordType))
            {
                func.RecordPointerParams.Add(param.Name);
                _recordLocals[param.Name] = recordType;
            }
        }

        foreach (var variable in proc.Variables)
        {
            if (variable is VariableNode varNode)
            {
                func.LocalValues.Add(new IrValue { Name = varNode.Name });
                func.LocalTypes[varNode.Name] = varNode.Type;
                if (_procedureTypeRegistry.Contains(varNode.Type))
                    _functionPointerNames.Add(varNode.Name);
                if (TryResolveRecordType(varNode.Type, out var recordType))
                {
                    _recordLocals[varNode.Name] = recordType;
                    func.LocalLayouts[varNode.Name] = new IrLocalLayout
                    {
                        ElementCount = 1,
                        ElementSizeBytes = recordType.SizeInBytes
                    };
                }
                else
                {
                    var elemSize = (TypeRegistry.Lookup(varNode.Type)?.BitWidth ?? 64) / 8;
                    func.LocalLayouts[varNode.Name] = new IrLocalLayout
                    {
                        ElementCount = varNode.ElementCount,
                        ElementSizeBytes = elemSize
                    };
                }
            }
        }

        var currentBlock = func.EntryBlock;
        foreach (var stmt in proc.Body)
            currentBlock = LowerStatement(stmt, currentBlock, func);

        func.EnsureBlocksRegistered();
        _activeConstTable = _programConstTable;
        return func;
    }

    private CompileTimeConstTable BuildProcedureConstTable(ProcedureNode proc)
    {
        var table = _programConstTable.Clone();
        foreach (var (name, value) in proc.ResolvedConstants)
            table.Define(name, value);
        return table;
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
            case BreakNode:
                if (_loopStack.Count == 0)
                    return block;
                block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = _loopStack.Peek().EndLabel });
                return block;
            case ContinueNode:
                if (_loopStack.Count == 0)
                    return block;
                block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = _loopStack.Peek().ContinueLabel });
                return block;
            case AssignExprNode assign:
                LowerAssignExpr(assign, block);
                return block;
            case LabelNode label:
                block.Add(WithSource(new IrInstruction(IrOpcode.InlineAsm, immediate: $"{label.Name}:"), label));
                return block;
        }
        return block;
    }

    private void LowerAssignExpr(AssignExprNode assign, IrBasicBlock block)
    {
        var dest = ResolveAssignTarget(assign.Target);
        LowerRuntimeExpression(assign.Expression, dest, block);
    }

    private IrValue ResolveAssignTarget(AstNode target)
    {
        return target switch
        {
            RegisterNode reg => GetOrCreateValue(reg.Name),
            IdentifierNode ident => GetOrCreateValue(ident.Name),
            _ => GetOrCreateValue("rax")
        };
    }

    private void LowerRuntimeExpression(AstNode expr, IrValue dest, IrBasicBlock block)
    {
        switch (expr)
        {
            case IntegerLiteralNode lit:
                block.Add(new IrInstruction(IrOpcode.LoadConstant, dest, immediate: lit.Value));
                return;
            case RegisterNode reg:
                block.Add(new IrInstruction(IrOpcode.Move, dest, new List<IrValue> { GetOrCreateValue(reg.Name) }));
                return;
            case IdentifierNode ident:
                block.Add(new IrInstruction(IrOpcode.Move, dest, new List<IrValue> { ResolveOperand(ident) }));
                return;
            case UnaryExprNode unary:
                LowerRuntimeUnary(unary, dest, block);
                return;
            case BinaryExprNode binary:
                LowerRuntimeBinary(binary, dest, block);
                return;
        }
    }

    private void LowerRuntimeUnary(UnaryExprNode unary, IrValue dest, IrBasicBlock block)
    {
        var scratchRax = GetOrCreateValue("rax");
        LowerRuntimeExpression(unary.Operand, scratchRax, block);

        if (unary.Operator == "-")
        {
            block.Add(new IrInstruction(IrOpcode.LoadConstant, GetOrCreateValue("rbx"), immediate: 0L));
            block.Add(new IrInstruction(IrOpcode.Subtract, scratchRax,
                new List<IrValue> { GetOrCreateValue("rbx") }));
        }
        else if (unary.Operator == "~")
        {
            block.Add(new IrInstruction(IrOpcode.BitwiseNot, scratchRax,
                new List<IrValue> { scratchRax }));
        }

        if (!SameValue(dest, scratchRax))
            block.Add(new IrInstruction(IrOpcode.Move, dest, new List<IrValue> { scratchRax }));
    }

    private void LowerRuntimeBinary(BinaryExprNode binary, IrValue dest, IrBasicBlock block)
    {
        var scratchRax = GetOrCreateValue("rax");
        var scratchRbx = GetOrCreateValue("rbx");

        if (IsComparisonOperator(binary.Operator))
        {
            LowerRuntimeExpression(binary.Left, scratchRax, block);
            LowerRuntimeExpression(binary.Right, scratchRbx, block);
            block.Add(new IrInstruction(IrOpcode.CompareToBool, dest,
                new List<IrValue> { scratchRax, scratchRbx })
            {
                CmpKind = MapComparisonOperator(binary.Operator)
            });
            return;
        }

        LowerRuntimeExpression(binary.Left, scratchRax, block);
        LowerRuntimeExpression(binary.Right, scratchRbx, block);

        var opcode = binary.Operator switch
        {
            "+" => IrOpcode.Add,
            "-" => IrOpcode.Subtract,
            "*" => IrOpcode.Multiply,
            "/" => IrOpcode.Divide,
            "%" => IrOpcode.Modulo,
            "&" => IrOpcode.BitwiseAnd,
            "|" => IrOpcode.BitwiseOr,
            "^" => IrOpcode.BitwiseXor,
            "<<" => IrOpcode.ShiftLeft,
            ">>" => IrOpcode.ShiftRight,
            _ => IrOpcode.Move
        };

        block.Add(new IrInstruction(opcode, scratchRax, new List<IrValue> { scratchRbx }));

        if (!SameValue(dest, scratchRax))
            block.Add(new IrInstruction(IrOpcode.Move, dest, new List<IrValue> { scratchRax }));
    }

    private static bool IsComparisonOperator(string op)
        => op is "==" or "!=" or "<" or "<=" or ">" or ">=" or "<?" or ">?";

    private static CompareKind MapComparisonOperator(string op) => op switch
    {
        "==" => CompareKind.Equal,
        "!=" => CompareKind.NotEqual,
        "<" => CompareKind.LessThanSigned,
        "<=" => CompareKind.LessOrEqualSigned,
        ">" => CompareKind.GreaterThanSigned,
        ">=" => CompareKind.GreaterOrEqualSigned,
        "<?" => CompareKind.LessThanUnsigned,
        ">?" => CompareKind.GreaterThanUnsigned,
        _ => CompareKind.Equal
    };

    private static bool SameValue(IrValue a, IrValue b)
        => string.Equals(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);

    private void LowerInstruction(InstructionNode instr, IrBasicBlock block)
    {
        var mnemonic = instr.Mnemonic.ToLowerInvariant();

        if (mnemonic == "jmp" && instr.Operands.Count == 1)
        {
            var target = FormatAsmOperand(instr.Operands[0]);
            block.Add(WithSource(new IrInstruction(IrOpcode.InlineAsm, immediate: $"    jmp {target}"), instr));
            return;
        }

        if (mnemonic == "idiv" && instr.Operands.Count == 2)
        {
            var divisor = ResolveOperand(instr.Operands[0]);
            var dest = ResolveOperand(instr.Operands[1]);
            block.Add(WithSource(new IrInstruction(IrOpcode.Divide, dest, new List<IrValue> { divisor }), instr));
            return;
        }

        if (mnemonic == "mod" && instr.Operands.Count == 2)
        {
            var divisor = ResolveOperand(instr.Operands[0]);
            var dest = ResolveOperand(instr.Operands[1]);
            block.Add(WithSource(new IrInstruction(IrOpcode.Modulo, dest, new List<IrValue> { divisor }), instr));
            return;
        }

        if (mnemonic is "idiv" or "div" && instr.Operands.Count == 1)
        {
            var divisor = FormatAsmOperand(instr.Operands[0]);
            var setup = mnemonic == "idiv" ? "    cqo" : "    xor rdx, rdx";
            block.Add(WithSource(new IrInstruction(IrOpcode.InlineAsm, immediate: $"{setup}\n    {mnemonic} {divisor}"), instr));
            return;
        }

        if (mnemonic == "div" && instr.Operands.Count == 2)
        {
            var divisor = FormatAsmOperand(instr.Operands[0]);
            var dest = FormatAsmOperand(instr.Operands[1]);
            block.Add(WithSource(new IrInstruction(IrOpcode.InlineAsm, immediate: FormatUnsignedDivide(dest, divisor)), instr));
            return;
        }

        if (instr.Operands.Count == 2)
        {
            var dst = ResolveOperand(instr.Operands[1]);
            var src = ResolveOperand(instr.Operands[0]);

            if (mnemonic == "xor" && IsSameReg(instr.Operands[0], instr.Operands[1]))
            {
                block.Add(WithSource(new IrInstruction(IrOpcode.LoadConstant, dst, immediate: 0L), instr));
                return;
            }

            var opcode = mnemonic switch
            {
                "mov" => IrOpcode.Move,
                "movsd" or "movss" or "movd" or "movq" or "addsd" or "subsd" or "ucomisd"
                    or "vaddpd" or "vmovapd" or "vxorpd" => IrOpcode.Move,
                "add" => IrOpcode.Add,
                "sub" => IrOpcode.Subtract,
                "imul" => IrOpcode.Multiply,
                "shl" => IrOpcode.ShiftLeft,
                "shr" or "sar" => IrOpcode.ShiftRight,
                "xor" => IrOpcode.BitwiseXor,
                "and" => IrOpcode.BitwiseAnd,
                "or" => IrOpcode.BitwiseOr,
                "cmp" => IrOpcode.Compare,
                _ => IrOpcode.Move
            };

            if (opcode == IrOpcode.Compare)
            {
                block.Add(WithSource(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { dst, src }), instr));
            }
            else
            {
                object? imm = mnemonic is "movsd" or "movss" or "movd" or "movq" or "addsd" or "subsd" or "ucomisd"
                    or "vaddpd" or "vmovapd" or "vxorpd"
                    ? mnemonic
                    : null;
                block.Add(WithSource(new IrInstruction(opcode, dst, new List<IrValue> { src }, immediate: imm), instr));
            }
        }
        else if (instr.Operands.Count == 1)
        {
            var op = ResolveOperand(instr.Operands[0]);
            if (mnemonic is "inc" or "dec")
            {
                var opcode = mnemonic == "inc" ? IrOpcode.Add : IrOpcode.Subtract;
                block.Add(WithSource(new IrInstruction(opcode, op, new List<IrValue> { new() { Name = "imm:1" } }), instr));
            }
            else
            {
                block.Add(WithSource(new IrInstruction(IrOpcode.Move, op, new List<IrValue> { op }), instr));
            }
        }
    }

    private static string FormatAsmOperand(AstNode op) => op switch
    {
        RegisterNode r => r.Name.ToLowerInvariant(),
        IdentifierNode id => id.Name,
        IntegerLiteralNode lit => lit.Value.ToString(),
        _ => "?"
    };

    private static string FormatUnsignedDivide(string dest, string divisor)
    {
        if (string.Equals(dest, "rax", StringComparison.OrdinalIgnoreCase))
            return $"    xor rdx, rdx\n    div {divisor}";
        return $"    mov rax, {dest}\n    xor rdx, rdx\n    div {divisor}\n    mov {dest}, rax";
    }

    private static IrInstruction WithSource(IrInstruction inst, AstNode node)
    {
        inst.SourceLine = node.Line;
        inst.SourceColumn = node.Column;
        return inst;
    }

    private static bool IsSameReg(AstNode a, AstNode b)
    {
        return a is RegisterNode ra && b is RegisterNode rb &&
               string.Equals(ra.Name, rb.Name, StringComparison.OrdinalIgnoreCase);
    }

    private void LowerCall(CallNode call, IrBasicBlock block)
    {
        if (call.Name is "stdout.put" or "stdout.putu")
        {
            var allArgs = new List<IrValue>();
            foreach (var arg in call.Arguments)
                allArgs.Add(ResolveOperand(arg));
            block.Add(new IrInstruction(IrOpcode.Call, operands: allArgs, immediate: call.Name));
            return;
        }

        if (BuiltinNames.IsBuiltin(call.Name))
        {
            var builtinArgs = new List<IrValue>();
            foreach (var arg in call.Arguments)
            {
                if (arg is IdentifierNode id &&
                    BuiltinNames.AtomicOrderings.Contains(id.Name))
                    builtinArgs.Add(new IrValue { Name = $"order:{id.Name.ToLowerInvariant()}" });
                else
                    builtinArgs.Add(ResolveOperand(arg));
            }
            block.Add(new IrInstruction(IrOpcode.Call, operands: builtinArgs, immediate: call.Name));
            return;
        }

        var callArgs = new List<IrValue>();
        foreach (var arg in call.Arguments)
            callArgs.Add(ResolveOperand(arg));

        string immediate;
        if (call.IsIndirect || _functionPointerNames.Contains(call.Name))
            immediate = $"indirect:{call.Name}";
        else if (_externRegistry.Contains(call.Name))
            immediate = $"extern:{call.Name}";
        else
            immediate = call.Name;

        block.Add(new IrInstruction(IrOpcode.Call, operands: callArgs, immediate: immediate));
    }

    private static CompareKind MapControlFlowCompareOperator(string op) => op switch
    {
        "=" or "==" => CompareKind.Equal,
        "!=" => CompareKind.NotEqual,
        "<" => CompareKind.LessThanSigned,
        "<=" => CompareKind.LessOrEqualSigned,
        ">" => CompareKind.GreaterThanSigned,
        ">=" => CompareKind.GreaterOrEqualSigned,
        "<?" => CompareKind.LessThanUnsigned,
        ">?" => CompareKind.GreaterThanUnsigned,
        _ => CompareKind.Equal
    };

    private static CompareKind InvertCompareKind(CompareKind kind) => kind switch
    {
        CompareKind.Equal => CompareKind.NotEqual,
        CompareKind.NotEqual => CompareKind.Equal,
        CompareKind.LessThanSigned => CompareKind.GreaterOrEqualSigned,
        CompareKind.LessOrEqualSigned => CompareKind.GreaterThanSigned,
        CompareKind.GreaterThanSigned => CompareKind.LessOrEqualSigned,
        CompareKind.GreaterOrEqualSigned => CompareKind.LessThanSigned,
        CompareKind.LessThanUnsigned => CompareKind.GreaterOrEqualUnsigned,
        CompareKind.LessOrEqualUnsigned => CompareKind.GreaterThanUnsigned,
        CompareKind.GreaterThanUnsigned => CompareKind.LessOrEqualUnsigned,
        CompareKind.GreaterOrEqualUnsigned => CompareKind.LessThanUnsigned,
        _ => CompareKind.NotEqual
    };

    private int _labelCounter;

    private IrBasicBlock LowerIf(IfNode ifNode, IrBasicBlock block, IrFunction func)
    {
        if (ifNode.Condition is not ComparisonNode comp)
            return block;

        var left = ResolveOperand(comp.Left);
        var right = ResolveOperand(comp.Right);

        var cmpKind = MapControlFlowCompareOperator(comp.Operator);

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

        block.Add(new IrInstruction(IrOpcode.ConditionalBranch)
        {
            TargetBlock = thenBlock.Label,
            CmpKind = cmpKind
        });

        block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = (elseBlock ?? contBlock).Label });

        var currentThen = thenBlock;
        foreach (var stmt in ifNode.ThenBody)
            currentThen = LowerStatement(stmt, currentThen, func);
        currentThen.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = endBlock.Label });

        if (elseBlock != null)
        {
            var currentElse = elseBlock;
            foreach (var stmt in ifNode.ElseBody)
                currentElse = LowerStatement(stmt, currentElse, func);
            currentElse.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = endBlock.Label });
        }

        func.AddBlock(endBlock);
        func.AddBlock(contBlock);
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

        // Register the header and body up front; the end/continuation blocks
        // are added AFTER the body is lowered so that any blocks created while
        // lowering the body (e.g. a nested if/else) are laid out before the
        // loop's continuation block. Otherwise the continuation block (which
        // carries the procedure tail) would physically precede those nested
        // blocks and fall through into them instead of reaching the epilogue.
        func.AddBlock(headerBlock);
        func.AddBlock(bodyBlock);

        block.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = headerBlock.Label });

        if (whileNode.Condition is ComparisonNode comp)
        {
            var left = ResolveOperand(comp.Left);
            var right = ResolveOperand(comp.Right);
            var cmpKind = MapControlFlowCompareOperator(comp.Operator);

            headerBlock.Add(new IrInstruction(IrOpcode.Compare, operands: new List<IrValue> { left, right })
            {
                CmpKind = cmpKind
            });

            var exitKind = InvertCompareKind(cmpKind);

            headerBlock.Add(new IrInstruction(IrOpcode.ConditionalBranch)
            {
                TargetBlock = endBlock.Label,
                CmpKind = exitKind
            });
        }

        headerBlock.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = bodyBlock.Label });

        _loopStack.Push((endBlock.Label, headerBlock.Label));
        var current = bodyBlock;
        foreach (var stmt in whileNode.Body)
            current = LowerStatement(stmt, current, func);
        _loopStack.Pop();
        current.Add(new IrInstruction(IrOpcode.Branch) { TargetBlock = headerBlock.Label });

        func.AddBlock(endBlock);
        func.AddBlock(contBlock);
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
            IdentifierNode ident when _activeConstTable.TryGetValue(ident.Name, out var constVal)
                => new IrValue { Name = $"imm:{constVal}" },
            IdentifierNode ident when _globalDataRegistry.Contains(ident.Name)
                => new IrValue { Name = GlobalDataEncoding.Encode(ident.Name) },
            IdentifierNode ident when _programProcedureNames.Contains(ident.Name)
                => new IrValue { Name = $"proc:{ident.Name}" },
            IdentifierNode ident => GetOrCreateValue(ident.Name),
            DotAccessNode dot => ResolveDotAccess(dot),
            AddressOfNode addr => new IrValue { Name = $"addr:{addr.VariableName}" },
            AddressOfStringNode addrStr => new IrValue { Name = AddressRefEncoding.EncodeString(addrStr.Value) },
            MemoryRefNode mem => new IrValue
            {
                Name = MemoryRefEncoding.Encode(mem.Register, mem.Offset, mem.SizeBits)
            },
            ArrayIndexNode arr => new IrValue { Name = EncodeArrayIndex(arr) },
            _ => new IrValue()
        };
    }

    private IrValue ResolveDotAccess(DotAccessNode dot)
    {
        if (_recordLocals.TryGetValue(dot.BaseName, out var record)
            && record.TryGetField(dot.MemberName, out var field))
        {
            return new IrValue
            {
                Name = FieldAccessEncoding.Encode(dot.BaseName, field.Offset, field.Type.BitWidth)
            };
        }

        var qualified = EnumTypeRegistry.QualifiedName(dot.BaseName, dot.MemberName);
        if (_activeConstTable.TryGetValue(qualified, out var enumVal))
            return new IrValue { Name = $"imm:{enumVal}" };

        return new IrValue { Name = "imm:0" };
    }

    private string EncodeArrayIndex(ArrayIndexNode arr)
    {
        if (TryResolveConstIndex(arr.Index, out var constIndex))
            return ArrayIndexEncoding.Encode(arr.ArrayName, new IntegerLiteralNode(constIndex));
        return ArrayIndexEncoding.Encode(arr.ArrayName, arr.Index);
    }

    private bool TryResolveConstIndex(AstNode index, out long value)
    {
        value = 0;
        if (index is IntegerLiteralNode lit)
        {
            value = lit.Value;
            return true;
        }

        if (index is IdentifierNode id && _activeConstTable.TryGetValue(id.Name, out value))
            return true;

        if (index is DotAccessNode dot)
        {
            var qualified = EnumTypeRegistry.QualifiedName(dot.BaseName, dot.MemberName);
            return _activeConstTable.TryGetValue(qualified, out value);
        }

        return false;
    }

    private bool TryResolveRecordType(string name, out RecordTypeSymbol record)
    {
        if (_procedureRecordTypes.TryGetValue(name, out record!))
            return true;
        return _recordRegistry.TryGet(name, out record!);
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