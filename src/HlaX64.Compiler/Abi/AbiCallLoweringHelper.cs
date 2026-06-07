using System.Text;
using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Abi;

internal static class AbiCallLoweringHelper
{
    internal sealed record CallTargetInfo(string Symbol, bool IsExtern, bool IsIndirect, string? IndirectVar);

    public static CallTargetInfo ParseCallTarget(object? immediate)
    {
        var raw = immediate as string ?? "";
        if (raw.StartsWith("extern:", StringComparison.Ordinal))
            return new CallTargetInfo(raw[7..], true, false, null);
        if (raw.StartsWith("indirect:", StringComparison.Ordinal))
            return new CallTargetInfo("", false, true, raw[9..]);
        return new CallTargetInfo(raw, false, false, null);
    }

    public static string ClassifyArgType(IrValue arg, IrFunction caller, ExternProcedureRegistry? externs,
        string calleeName, int argIndex, ProcedureTypeRegistry? procTypes, RecordTypeRegistry? records)
    {
        if (arg.Name != null && caller.LocalTypes.TryGetValue(arg.Name, out var localType))
            return localType;

        if (externs?.TryGet(calleeName, out var ext) == true && argIndex < ext.Parameters.Count)
            return ext.Parameters[argIndex].ParamType;

        return "int64";
    }

    public static void AppendSysVCall(StringBuilder sb, IrInstruction inst, IrFunction caller,
        Func<IrValue?, string> resolve,
        ExternProcedureRegistry? externs, ProcedureTypeRegistry? procTypes, RecordTypeRegistry? records,
        List<string> externsOut)
    {
        var target = ParseCallTarget(inst.Immediate);
        if (target.IsExtern && externs?.TryGet(target.Symbol, out var ext) == true && ext.IsVariadic)
        {
            AppendSysVVariadicCall(sb, inst, caller, resolve, externs, procTypes, records, externsOut, target);
            return;
        }

        if (target.IsExtern)
            externsOut.Add(target.Symbol);

        var paramTypes = BuildArgTypes(inst, caller, target, externs, procTypes, records);
        var classified = AbiArgumentClassifier.ClassifyParameters(paramTypes, records, procTypes);
        AbiArgumentClassifier.AssignSysVRegisters(classified,
            out var gprAssign, out var xmmAssign);

        int regGpr = 0;
        int regXmm = 0;
        string[] gprs = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];
        string[] xms = ["xmm0", "xmm1", "xmm2", "xmm3", "xmm4", "xmm5", "xmm6", "xmm7"];

        var stackArgs = new List<(IrValue Arg, AbiArgClass Class)>();
        for (int i = 0; i < inst.Operands.Count; i++)
        {
            var arg = inst.Operands[i];
            var cls = i < classified.Count ? classified[i].Class : AbiArgClass.Integer;
            bool placed = false;
            if (cls is AbiArgClass.Float32 or AbiArgClass.Float64)
            {
                if (regXmm < xms.Length) { regXmm++; placed = true; }
            }
            else if (regGpr < gprs.Length)
            {
                regGpr++;
                placed = true;
            }

            if (!placed)
                stackArgs.Add((arg, cls));
        }

        for (int i = stackArgs.Count - 1; i >= 0; i--)
        {
            var val = resolve(stackArgs[i].Arg);
            sb.AppendLine($"    push {val}      ; stack arg");
        }

        regGpr = 0;
        regXmm = 0;
        for (int i = 0; i < inst.Operands.Count; i++)
        {
            var arg = inst.Operands[i];
            var val = resolve(arg);
            var cls = i < classified.Count ? classified[i].Class : AbiArgClass.Integer;
            if (cls is AbiArgClass.Float32 or AbiArgClass.Float64)
            {
                if (regXmm < xms.Length)
                {
                    var xmm = xms[regXmm++];
                    var mov = cls == AbiArgClass.Float32 ? "movss" : "movsd";
                    sb.AppendLine($"    {mov} {xmm}, {val}");
                }
            }
            else if (regGpr < gprs.Length)
            {
                var gpr = gprs[regGpr++];
                if (IsProcedureRef(arg))
                    sb.AppendLine($"    lea {gpr}, [rel {ProcedureRefName(arg)}]");
                else
                    sb.AppendLine($"    mov {gpr}, {val}");
            }
        }

        int alignPad = stackArgs.Count == 0 ? 8 : (stackArgs.Count % 2 == 1 ? 8 : 0);
        if (alignPad > 0)
            sb.AppendLine($"    sub rsp, {alignPad}      ; align stack");

        if (target.IsIndirect)
        {
            sb.AppendLine($"    mov rax, {resolve(new IrValue { Name = target.IndirectVar })}");
            sb.AppendLine("    call rax");
        }
        else
        {
            sb.AppendLine($"    call {target.Symbol}");
        }

        int cleanup = stackArgs.Count * 8 + alignPad;
        sb.Append($"    add rsp, {cleanup}      ; restore stack");
    }

    private static void AppendSysVVariadicCall(StringBuilder sb, IrInstruction inst, IrFunction caller,
        Func<IrValue?, string> resolve,
        ExternProcedureRegistry? externs, ProcedureTypeRegistry? procTypes, RecordTypeRegistry? records,
        List<string> externsOut, CallTargetInfo target)
    {
        externsOut.Add(target.Symbol);
        string[] gprs = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];
        int regGpr = 0;
        int regXmm = 0;
        var stackArgs = new List<IrValue>();

        for (int i = 0; i < inst.Operands.Count; i++)
        {
            if (regGpr < gprs.Length)
                regGpr++;
            else
                stackArgs.Add(inst.Operands[i]);
        }

        for (int i = stackArgs.Count - 1; i >= 0; i--)
            sb.AppendLine($"    push {resolve(stackArgs[i])}      ; variadic stack arg");

        regGpr = 0;
        for (int i = 0; i < inst.Operands.Count; i++)
        {
            var val = resolve(inst.Operands[i]);
            if (regGpr < gprs.Length)
            {
                var gpr = gprs[regGpr++];
                if (IsProcedureRef(inst.Operands[i]))
                    sb.AppendLine($"    lea {gpr}, [rel {ProcedureRefName(inst.Operands[i])}]");
                else if (IsStringRef(inst.Operands[i]))
                    sb.AppendLine($"    lea {gpr}, [{resolve(inst.Operands[i])}]");
                else
                    sb.AppendLine($"    mov {gpr}, {val}");
            }
        }

        sb.AppendLine($"    mov al, {regXmm}      ; SSE register count for variadic");
        int alignPad = stackArgs.Count == 0 ? 8 : (stackArgs.Count % 2 == 1 ? 8 : 0);
        if (alignPad > 0)
            sb.AppendLine($"    sub rsp, {alignPad}      ; align stack");

        sb.AppendLine($"    call {target.Symbol}");

        int cleanup = stackArgs.Count * 8 + alignPad;
        sb.Append($"    add rsp, {cleanup}      ; restore stack");
    }

    private static bool IsStringRef(IrValue arg)
        => arg.Name?.StartsWith("str:", StringComparison.Ordinal) == true
           || arg.Name?.StartsWith("addr:", StringComparison.Ordinal) == true;

    public static void AppendWindowsCall(StringBuilder sb, IrInstruction inst, IrFunction caller,
        Func<IrValue?, string> resolve,
        ExternProcedureRegistry? externs, ProcedureTypeRegistry? procTypes, RecordTypeRegistry? records,
        List<string> externsOut)
    {
        var target = ParseCallTarget(inst.Immediate);
        if (target.IsExtern)
            externsOut.Add(target.Symbol);

        var paramTypes = BuildArgTypes(inst, caller, target, externs, procTypes, records);
        var classified = AbiArgumentClassifier.ClassifyParameters(paramTypes, records, procTypes);

        int regGpr = 0;
        int regXmm = 0;
        string[] gprs = ["rcx", "rdx", "r8", "r9"];
        string[] xms = ["xmm0", "xmm1", "xmm2", "xmm3"];
        int stackArgCount = 0;

        for (int i = 0; i < inst.Operands.Count; i++)
        {
            var cls = i < classified.Count ? classified[i].Class : AbiArgClass.Integer;
            if (cls is AbiArgClass.Float32 or AbiArgClass.Float64)
            {
                if (regXmm >= xms.Length) stackArgCount++;
                else regXmm++;
            }
            else if (regGpr >= gprs.Length)
                stackArgCount++;
            else
                regGpr++;
        }

        int shadowAndStack = 32 + stackArgCount * 8;
        if (shadowAndStack > 0)
            sb.AppendLine($"    sub rsp, {shadowAndStack}      ; shadow + stack args");

        regGpr = 0;
        regXmm = 0;
        int stackIdx = 0;
        for (int i = 0; i < inst.Operands.Count; i++)
        {
            var arg = inst.Operands[i];
            var val = resolve(arg);
            var cls = i < classified.Count ? classified[i].Class : AbiArgClass.Integer;
            if (cls is AbiArgClass.Float32 or AbiArgClass.Float64)
            {
                if (regXmm < xms.Length)
                {
                    var xmm = xms[regXmm++];
                    var mov = cls == AbiArgClass.Float32 ? "movss" : "movsd";
                    sb.AppendLine($"    {mov} {xmm}, {val}");
                }
                else
                {
                    var mov = cls == AbiArgClass.Float32 ? "movss" : "movsd";
                    sb.AppendLine($"    {mov} [{stackIdx} + rsp + 32], {val}");
                    stackIdx += 8;
                }
            }
            else if (regGpr < gprs.Length)
            {
                var gpr = gprs[regGpr++];
                if (IsProcedureRef(arg))
                    sb.AppendLine($"    lea {gpr}, [rel {ProcedureRefName(arg)}]");
                else
                    sb.AppendLine($"    mov {gpr}, {val}");
            }
            else
            {
                sb.AppendLine($"    mov qword [{stackIdx} + rsp + 32], {val}");
                stackIdx += 8;
            }
        }

        if (target.IsIndirect)
        {
            sb.AppendLine($"    mov rax, {resolve(new IrValue { Name = target.IndirectVar })}");
            sb.AppendLine("    call rax");
        }
        else
        {
            sb.AppendLine($"    call {target.Symbol}");
        }

        if (shadowAndStack > 0)
            sb.Append($"    add rsp, {shadowAndStack}      ; restore shadow + stack");
    }

    private static List<(string Name, string Type)> BuildArgTypes(IrInstruction inst, IrFunction caller,
        CallTargetInfo target, ExternProcedureRegistry? externs, ProcedureTypeRegistry? procTypes,
        RecordTypeRegistry? records)
    {
        var result = new List<(string, string)>();
        for (int i = 0; i < inst.Operands.Count; i++)
        {
            var arg = inst.Operands[i];
            var type = ClassifyArgType(arg, caller, externs, target.Symbol, i, procTypes, records);
            result.Add(($"arg{i}", type));
        }

        return result;
    }

    private static bool IsXmmReg(string operand)
        => operand.StartsWith("xmm", StringComparison.OrdinalIgnoreCase);

    private static bool IsProcedureRef(IrValue arg)
        => arg.Name?.StartsWith("proc:", StringComparison.Ordinal) == true;

    private static string ProcedureRefName(IrValue arg) => arg.Name![5..];
}
