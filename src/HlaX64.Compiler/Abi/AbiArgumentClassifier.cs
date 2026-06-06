using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Abi;

public enum AbiArgClass
{
    Integer,
    Pointer,
    Float32,
    Float64,
    RecordByPointer
}

public sealed record AbiParamInfo(string Name, string TypeName, AbiArgClass Class, int Index);

/// <summary>Classifies parameters for SysV/Win64 mixed integer and SSE argument passing.</summary>
public static class AbiArgumentClassifier
{
    public static AbiArgClass ClassifyType(string typeName, RecordTypeRegistry? records = null,
        ProcedureTypeRegistry? procTypes = null)
    {
        if (TypeRegistry.IsFloat(typeName))
            return TypeRegistry.LookupFloat(typeName)!.BitWidth == 32 ? AbiArgClass.Float32 : AbiArgClass.Float64;

        if (procTypes?.Contains(typeName) == true || string.Equals(typeName, "ptr", StringComparison.OrdinalIgnoreCase))
            return AbiArgClass.Pointer;

        if (records != null && records.TryGet(typeName, out _))
            return AbiArgClass.RecordByPointer;

        return AbiArgClass.Integer;
    }

    public static List<AbiParamInfo> ClassifyParameters(
        IReadOnlyList<(string Name, string Type)> parameters,
        RecordTypeRegistry? records = null,
        ProcedureTypeRegistry? procTypes = null)
    {
        var result = new List<AbiParamInfo>(parameters.Count);
        for (int i = 0; i < parameters.Count; i++)
        {
            var (name, type) = parameters[i];
            result.Add(new AbiParamInfo(name, type, ClassifyType(type, records, procTypes), i));
        }

        return result;
    }

    public static void AssignSysVRegisters(IReadOnlyList<AbiParamInfo> parameters,
        out List<(AbiParamInfo Param, string GprReg)> gprAssignments,
        out List<(AbiParamInfo Param, string XmmReg)> xmmAssignments)
    {
        gprAssignments = new();
        xmmAssignments = new();
        int gpr = 0;
        int xmm = 0;
        string[] gprs = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"];
        string[] xms = ["xmm0", "xmm1", "xmm2", "xmm3", "xmm4", "xmm5", "xmm6", "xmm7"];

        foreach (var param in parameters)
        {
            if (param.Class is AbiArgClass.Float32 or AbiArgClass.Float64)
            {
                if (xmm < xms.Length)
                    xmmAssignments.Add((param, xms[xmm++]));
            }
            else
            {
                if (gpr < gprs.Length)
                    gprAssignments.Add((param, gprs[gpr++]));
            }
        }
    }

    public static void AssignWindowsRegisters(IReadOnlyList<AbiParamInfo> parameters,
        out List<(AbiParamInfo Param, string GprReg)> gprAssignments,
        out List<(AbiParamInfo Param, string XmmReg)> xmmAssignments)
    {
        gprAssignments = new();
        xmmAssignments = new();
        int gpr = 0;
        int xmm = 0;
        string[] gprs = ["rcx", "rdx", "r8", "r9"];
        string[] xms = ["xmm0", "xmm1", "xmm2", "xmm3"];

        foreach (var param in parameters)
        {
            if (param.Class is AbiArgClass.Float32 or AbiArgClass.Float64)
            {
                if (xmm < xms.Length)
                    xmmAssignments.Add((param, xms[xmm++]));
            }
            else
            {
                if (gpr < gprs.Length)
                    gprAssignments.Add((param, gprs[gpr++]));
            }
        }
    }
}
