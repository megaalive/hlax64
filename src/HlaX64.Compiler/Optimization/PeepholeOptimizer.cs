using HlaX64.Compiler.Abi;

namespace HlaX64.Compiler.Optimization;

public static class PeepholeOptimizer
{
    public static void OptimizeLowered(IReadOnlyList<LoweredFunction> functions)
    {
        foreach (var func in functions)
        {
            foreach (var block in func.Blocks)
                block.Instructions.RemoveAll(IsNoOp);
        }
    }

    private static bool IsNoOp(LoweredInstruction inst)
    {
        var text = inst.AsmText.Trim();
        if (text.StartsWith(';')) return false;

        if (text.Equals("mov rax, rax", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("mov rbx, rbx", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("add ", StringComparison.OrdinalIgnoreCase) &&
            text.EndsWith(", 0", StringComparison.Ordinal))
            return true;

        if (text.StartsWith("add qword ", StringComparison.OrdinalIgnoreCase) &&
            text.EndsWith(", 0", StringComparison.Ordinal))
            return true;

        return false;
    }
}
