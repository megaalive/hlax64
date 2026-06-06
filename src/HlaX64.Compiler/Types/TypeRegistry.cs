namespace HlaX64.Compiler.Types;

public static class TypeRegistry
{
    public static readonly IntegerTypeSymbol Int8 = new("int8", 8, true);
    public static readonly IntegerTypeSymbol Int16 = new("int16", 16, true);
    public static readonly IntegerTypeSymbol Int32 = new("int32", 32, true);
    public static readonly IntegerTypeSymbol Int64 = new("int64", 64, true);
    public static readonly IntegerTypeSymbol UInt8 = new("uint8", 8, false);
    public static readonly IntegerTypeSymbol UInt16 = new("uint16", 16, false);
    public static readonly IntegerTypeSymbol UInt32 = new("uint32", 32, false);
    public static readonly IntegerTypeSymbol UInt64 = new("uint64", 64, false);

    public static readonly IntegerTypeSymbol Byte = new("byte", 8, false);
    public static readonly IntegerTypeSymbol Word = new("word", 16, false);
    public static readonly IntegerTypeSymbol DWord = new("dword", 32, false);
    public static readonly IntegerTypeSymbol QWord = new("qword", 64, false);
    public static readonly IntegerTypeSymbol Ptr = new("ptr", 64, false);
    public static readonly IntegerTypeSymbol CString = new("cstring", 64, false);

    private static readonly Dictionary<string, IntegerTypeSymbol> _types = new()
    {
        ["int8"] = Int8,
        ["int16"] = Int16,
        ["int32"] = Int32,
        ["int64"] = Int64,
        ["uint8"] = UInt8,
        ["uint16"] = UInt16,
        ["uint32"] = UInt32,
        ["uint64"] = UInt64,
        ["byte"] = Byte,
        ["word"] = Word,
        ["dword"] = DWord,
        ["qword"] = QWord,
        ["ptr"] = Ptr,
        ["cstring"] = CString,
    };

    public static IntegerTypeSymbol? Lookup(string name)
    {
        _types.TryGetValue(name.ToLowerInvariant(), out var type);
        return type;
    }

    public static bool CanImplicitlyConvert(IntegerTypeSymbol from, IntegerTypeSymbol to)
    {
        if (from == to)
            return true;
        if (from.BitWidth < to.BitWidth)
            return true;
        if (!from.IsSigned && to.IsSigned && from.BitWidth <= to.BitWidth)
            return true;
        return false;
    }
}
