using System.Text.RegularExpressions;
using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Optimization;

public static class PeepholeOptimizer
{
    private static readonly Regex MovZeroPattern = new(
        @"^\s*mov\s+(?<reg>r[a-z0-9]+|e[a-z]{3}|r\d+d)\s*,\s*0(x0)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static void OptimizeLowered(IReadOnlyList<LoweredFunction> functions, OptimizationLevel level = OptimizationLevel.Basic)
    {
        foreach (var func in functions)
        {
            foreach (var block in func.Blocks)
            {
                for (int i = 0; i < block.Instructions.Count; i++)
                {
                    var inst = block.Instructions[i];
                    if (IsNoOp(inst))
                    {
                        block.Instructions.RemoveAt(i);
                        i--;
                        continue;
                    }

                    if (level >= OptimizationLevel.Aggressive)
                    {
                        var rewritten = TryRewriteMovZeroToXor(inst);
                        if (rewritten != null)
                            block.Instructions[i] = rewritten;
                    }
                }
            }
        }
    }

    private static LoweredInstruction? TryRewriteMovZeroToXor(LoweredInstruction inst)
    {
        var match = MovZeroPattern.Match(inst.AsmText);
        if (!match.Success)
            return null;

        var reg = match.Groups["reg"].Value.ToLowerInvariant();
        return new LoweredInstruction($"    xor {reg}, {reg}")
        {
            IrId = inst.IrId,
            SourceLine = inst.SourceLine,
            NasmLabel = inst.NasmLabel
        };
    }

    private static bool IsNoOp(LoweredInstruction inst)
    {
        var text = inst.AsmText.Trim();
        if (text.StartsWith(';')) return false;

        if (text.Equals("mov rax, rax", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("mov rbx, rbx", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("mov rcx, rcx", StringComparison.OrdinalIgnoreCase) ||
            text.Equals("mov rdx, rdx", StringComparison.OrdinalIgnoreCase))
            return true;

        if (text.StartsWith("add ", StringComparison.OrdinalIgnoreCase) &&
            text.EndsWith(", 0", StringComparison.Ordinal))
            return true;

        if (text.StartsWith("add qword ", StringComparison.OrdinalIgnoreCase) &&
            text.EndsWith(", 0", StringComparison.Ordinal))
            return true;

        if (text.StartsWith("mov ", StringComparison.OrdinalIgnoreCase))
        {
            var parts = text.Split(',');
            if (parts.Length == 2)
            {
                var dest = parts[0]["mov ".Length..].Trim();
                var src = parts[1].Trim();
                if (dest.Equals(src, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }

        return false;
    }
}
