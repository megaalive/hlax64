using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Verification;

public sealed record AbiParamReport(
    string Name,
    string Type,
    string? Register,
    int? StackIndex);

public sealed record AbiProcedureReport(
    string Name,
    string? ReturnRegister,
    IReadOnlyList<AbiParamReport> Parameters,
    bool IsExport,
    bool IsEntryPoint);

public sealed record AbiVerificationResult(
    bool Success,
    string Target,
    IReadOnlyList<AbiProcedureReport> Procedures,
    IReadOnlyList<string> ExternSymbols,
    IReadOnlyList<string> Issues);

/// <summary>
/// Reports ABI conformance per procedure (param registers, stack slots, return reg, externs).
/// </summary>
public static class AbiVerifier
{
    public static AbiVerificationResult Verify(
        CompilationOptions options,
        IReadOnlyList<IrFunction> functions,
        ExternProcedureRegistry externs,
        RecordTypeRegistry records,
        ProcedureTypeRegistry procedureTypes)
    {
        var issues = new List<string>();
        var procedures = new List<AbiProcedureReport>();
        var isWindows = options.Target.Abi.Equals("msabi", StringComparison.OrdinalIgnoreCase);
        var argRegs = isWindows
            ? new[] { "rcx", "rdx", "r8", "r9" }
            : new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
        var returnReg = "rax";

        foreach (var fn in functions.Where(f => !f.IsEntryPoint))
        {
            var paramTypes = fn.ParameterTypes.Select(p => (p.Name, p.Type)).ToList();
            var classified = AbiArgumentClassifier.ClassifyParameters(paramTypes, records, procedureTypes);

            List<(AbiParamInfo Param, string GprReg)> gpr;
            List<(AbiParamInfo Param, string XmmReg)> xmm;
            if (isWindows)
                AbiArgumentClassifier.AssignWindowsRegisters(classified, out gpr, out xmm);
            else
                AbiArgumentClassifier.AssignSysVRegisters(classified, out gpr, out xmm);

            var regMap = gpr.ToDictionary(x => x.Param.Index, x => x.GprReg, comparer: EqualityComparer<int>.Default);
            foreach (var x in xmm)
                regMap[x.Param.Index] = x.XmmReg;

            var assigned = new HashSet<int>(regMap.Keys);
            var paramReports = new List<AbiParamReport>();
            for (int i = 0; i < classified.Count; i++)
            {
                var p = classified[i];
                regMap.TryGetValue(i, out var reg);
                int? stackIndex = assigned.Contains(i)
                    ? null
                    : i - argRegs.Length;
                paramReports.Add(new AbiParamReport(p.Name, p.TypeName, reg, stackIndex));
            }

            var ret = fn.ReturnsRegister ?? returnReg;
            procedures.Add(new AbiProcedureReport(fn.Name, ret, paramReports, fn.IsExport, fn.IsEntryPoint));
        }

        var externSymbols = externs.All.Select(e => e.Name).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        if (functions.All(f => f.IsEntryPoint || f.ParameterTypes.Count == 0) && externSymbols.Count == 0)
        {
            // no issue — empty programs are fine
        }

        return new AbiVerificationResult(issues.Count == 0, options.Target.ToString(), procedures, externSymbols, issues);
    }
}
