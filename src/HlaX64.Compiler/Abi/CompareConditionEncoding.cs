using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Abi;

internal static class CompareConditionEncoding
{
    internal static string SetccMnemonic(CompareKind? kind) => kind switch
    {
        CompareKind.Equal => "sete",
        CompareKind.NotEqual => "setne",
        CompareKind.LessThanSigned => "setl",
        CompareKind.LessOrEqualSigned => "setle",
        CompareKind.GreaterThanSigned => "setg",
        CompareKind.GreaterOrEqualSigned => "setge",
        CompareKind.LessThanUnsigned => "setb",
        CompareKind.LessOrEqualUnsigned => "setbe",
        CompareKind.GreaterThanUnsigned => "seta",
        CompareKind.GreaterOrEqualUnsigned => "setae",
        _ => "sete"
    };
}
