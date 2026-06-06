using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Types;

namespace HlaX64.Compiler.Abi;

public interface IAbiLowerer
{
    string Name { get; }
    IReadOnlyList<string> ArgumentRegisters { get; }
    string ReturnRegister { get; }
    IReadOnlyList<string> CallerSaved { get; }
    IReadOnlyList<string> CalleeSaved { get; }
    int StackAlignment { get; }
    IReadOnlyList<StringLiteralInfo> StringLiterals { get; }
    LoweredFunction Lower(IrFunction function, CompilationOptions options,
        IReadOnlyDictionary<string, GlobalDataSymbol>? globalData = null,
        ProcedureTypeRegistry? procedureTypes = null,
        RecordTypeRegistry? recordTypes = null,
        ExternProcedureRegistry? externProcedures = null);
}
