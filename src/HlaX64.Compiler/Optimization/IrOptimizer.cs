using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Optimization;

public static class IrOptimizer
{
    public static void Optimize(IReadOnlyList<IrFunction> functions, OptimizationLevel level)
    {
        if (level == OptimizationLevel.None) return;

        foreach (var func in functions)
            OptimizeFunction(func);

        if (level >= OptimizationLevel.Aggressive)
        {
            foreach (var func in functions)
                PropagateConstants(func);
        }
    }

    private static void OptimizeFunction(IrFunction func)
    {
        foreach (var block in func.Blocks)
            FoldConstantsInBlock(block);
    }

    private static void PropagateConstants(IrFunction func)
    {
        foreach (var block in func.Blocks)
        {
            var constants = new Dictionary<IrValue, long>();
            for (int i = 0; i < block.Instructions.Count; i++)
            {
                var inst = block.Instructions[i];
                if (inst.Opcode == IrOpcode.LoadConstant && inst.Destination != null && inst.Immediate is long v)
                {
                    constants[inst.Destination] = v;
                    continue;
                }

                if (inst.Opcode == IrOpcode.Move && inst.Destination != null && inst.Operands.Count == 1)
                {
                    if (constants.TryGetValue(inst.Operands[0], out var imm))
                    {
                        block.Instructions[i] = new IrInstruction(IrOpcode.LoadConstant, inst.Destination, immediate: imm)
                        {
                            SourceLine = inst.SourceLine,
                            SourceColumn = inst.SourceColumn
                        };
                        constants[inst.Destination] = imm;
                        continue;
                    }
                }

                if (inst.Destination != null && inst.Opcode is not IrOpcode.LoadConstant)
                    constants.Remove(inst.Destination);
            }
        }
    }

    private static void FoldConstantsInBlock(IrBasicBlock block)
    {
        var constants = new Dictionary<IrValue, long>();
        var newInstructions = new List<IrInstruction>();

        foreach (var inst in block.Instructions)
        {
            if (inst.Opcode == IrOpcode.LoadConstant && inst.Destination != null && inst.Immediate is long v)
            {
                constants[inst.Destination] = v;
                newInstructions.Add(inst);
                continue;
            }

            if (inst.Opcode is IrOpcode.Add or IrOpcode.Subtract &&
                inst.Destination != null &&
                inst.Operands.Count == 1 &&
                constants.TryGetValue(inst.Destination, out var left) &&
                TryParseImmediate(inst.Operands[0], out var right))
            {
                var folded = inst.Opcode == IrOpcode.Add ? left + right : left - right;
                constants[inst.Destination] = folded;
                newInstructions.Add(new IrInstruction(IrOpcode.LoadConstant, inst.Destination, immediate: folded)
                {
                    SourceLine = inst.SourceLine,
                    SourceColumn = inst.SourceColumn
                });
                continue;
            }

            if (inst.Opcode == IrOpcode.Move && inst.Destination != null && inst.Operands.Count == 1)
            {
                if (constants.TryGetValue(inst.Operands[0], out var moved))
                {
                    constants[inst.Destination] = moved;
                    newInstructions.Add(new IrInstruction(IrOpcode.LoadConstant, inst.Destination, immediate: moved)
                    {
                        SourceLine = inst.SourceLine,
                        SourceColumn = inst.SourceColumn
                    });
                    continue;
                }

                if (TryParseImmediate(inst.Operands[0], out var imm))
                    constants[inst.Destination] = imm;
                else
                    constants.Remove(inst.Destination);
            }
            else if (inst.Destination != null && inst.Opcode is not IrOpcode.LoadConstant)
            {
                constants.Remove(inst.Destination);
            }

            newInstructions.Add(inst);
        }

        block.Instructions.Clear();
        block.Instructions.AddRange(newInstructions);
    }

    private static bool TryParseImmediate(IrValue value, out long result)
    {
        result = 0;
        if (value.Name != null && value.Name.StartsWith("imm:", StringComparison.Ordinal) &&
            long.TryParse(value.Name["imm:".Length..], out result))
            return true;
        return false;
    }
}
