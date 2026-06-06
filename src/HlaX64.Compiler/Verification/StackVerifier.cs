using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Verification;

public sealed record StackVerificationIssue(
    string Code,
    string Message,
    string Procedure);

public sealed record StackVerificationResult(
    bool Success,
    IReadOnlyList<StackVerificationIssue> Issues,
    IReadOnlyList<StackVerificationReport> Procedures);

public sealed record StackVerificationReport(
    string Procedure,
    int StackFrameSizeBytes,
    int LocalSlotCount,
    bool HasPrologue,
    bool HasEpilogue,
    bool AlignmentOk);

/// <summary>
/// Verifies stack frame layout and prologue/epilogue on lowered functions (RFC 0014 / Phase 18).
/// </summary>
public static class StackVerifier
{
    public static StackVerificationResult Verify(
        IReadOnlyList<IrFunction> irFunctions,
        IReadOnlyList<LoweredFunction> loweredFunctions)
    {
        var issues = new List<StackVerificationIssue>();
        var reports = new List<StackVerificationReport>();
        var loweredByName = loweredFunctions.ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var ir in irFunctions)
        {
            var stackMap = ProcedureStackMap.Build(ir);
            VerifySlotLayout(ir, stackMap, issues);

            if (!loweredByName.TryGetValue(ir.Name, out var lowered))
            {
                issues.Add(new StackVerificationIssue("HLAX0064",
                    $"Missing lowered function for '{ir.Name}'", ir.Name));
                continue;
            }

            var hasPrologue = HasPrologue(lowered, ir.IsEntryPoint);
            var hasEpilogue = HasEpilogue(lowered, ir.IsEntryPoint);
            var alignmentOk = lowered.StackFrameSize % 16 == 0
                || (ir.IsEntryPoint && lowered.StackFrameSize == 8);

            if (!ir.IsEntryPoint && !hasPrologue)
            {
                issues.Add(new StackVerificationIssue("HLAX0065",
                    $"Procedure '{ir.Name}' missing stack prologue (push rbp / mov rbp, rsp)", ir.Name));
            }

            if (!hasEpilogue && !EndsWithBranch(lowered) && !ir.IsEntryPoint)
            {
                issues.Add(new StackVerificationIssue("HLAX0066",
                    $"Procedure '{ir.Name}' missing stack epilogue before return", ir.Name));
            }

            if (!alignmentOk)
            {
                issues.Add(new StackVerificationIssue("HLAX0067",
                    $"Stack frame size {lowered.StackFrameSize} is not 16-byte aligned for '{ir.Name}'", ir.Name));
            }

            var expectedFrame = Align16(stackMap.StackOffsetBytes);
            if (!ir.IsEntryPoint && lowered.StackFrameSize != expectedFrame && stackMap.StackOffsetBytes > 0)
            {
                issues.Add(new StackVerificationIssue("HLAX0068",
                    $"Stack frame metadata mismatch for '{ir.Name}': lowered {lowered.StackFrameSize} vs layout {expectedFrame}",
                    ir.Name));
            }

            reports.Add(new StackVerificationReport(
                ir.Name,
                lowered.StackFrameSize,
                stackMap.Layouts.Count,
                hasPrologue,
                hasEpilogue,
                alignmentOk));
        }

        return new StackVerificationResult(issues.Count == 0, issues, reports);
    }

    private static void VerifySlotLayout(IrFunction ir, ProcedureStackMap stackMap, List<StackVerificationIssue> issues)
    {
        int nextSlot = ir.ParameterValues.Count;
        foreach (var local in ir.LocalValues)
        {
            if (!stackMap.Layouts.TryGetValue(local.Name!, out var layout))
                continue;

            nextSlot += layout.StackSlots;
        }

        var usedBytes = nextSlot * 8;
        if (usedBytes < stackMap.StackOffsetBytes)
        {
            issues.Add(new StackVerificationIssue("HLAX0064",
                $"Overlapping or inconsistent stack slot layout in '{ir.Name}'", ir.Name));
        }
    }

    private static bool HasPrologue(LoweredFunction fn, bool isEntry)
    {
        var text = string.Join('\n', fn.Blocks.SelectMany(b => b.Instructions).Select(i => i.AsmText));
        if (isEntry)
            return text.Contains("push rbp", StringComparison.OrdinalIgnoreCase);

        return text.Contains("push rbp", StringComparison.OrdinalIgnoreCase)
            && text.Contains("mov rbp, rsp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasEpilogue(LoweredFunction fn, bool isEntry)
    {
        var text = string.Join('\n', fn.Blocks.SelectMany(b => b.Instructions).Select(i => i.AsmText));
        if (isEntry)
            return text.Contains("leave", StringComparison.OrdinalIgnoreCase)
                || (text.Contains("pop rbp", StringComparison.OrdinalIgnoreCase)
                    && text.Contains("mov rsp", StringComparison.OrdinalIgnoreCase));

        return text.Contains("leave", StringComparison.OrdinalIgnoreCase)
            || text.Contains("mov rsp, rbp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool EndsWithBranch(LoweredFunction fn)
    {
        if (fn.Blocks.Count == 0)
            return false;

        var last = fn.Blocks[^1].Instructions.LastOrDefault()?.AsmText ?? "";
        return last.Contains("jmp", StringComparison.OrdinalIgnoreCase)
            || last.Contains("ret", StringComparison.OrdinalIgnoreCase);
    }

    private static int Align16(int bytes) => ((bytes + 15) / 16) * 16;
}
