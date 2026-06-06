using HlaX64.Compiler.Ast;
using HlaX64.Compiler.Diagnostics;
using HlaX64.Compiler.Semantic;

namespace HlaX64.Compiler.Types;

public sealed class EnumTypeSymbol
{
    public required string Name { get; init; }
    public required IntegerTypeSymbol BackingType { get; init; }
    public required IReadOnlyDictionary<string, long> Members { get; init; }
}

public sealed class EnumTypeRegistry
{
    private readonly Dictionary<string, EnumTypeSymbol> _enums = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, EnumTypeSymbol> Enums => _enums;

    public void Clear() => _enums.Clear();

    public bool TryGet(string name, out EnumTypeSymbol enumType)
        => _enums.TryGetValue(name, out enumType!);

    public bool Contains(string name) => _enums.ContainsKey(name);

    public bool TryGetMemberValue(string enumName, string memberName, out long value)
    {
        value = 0;
        if (!_enums.TryGetValue(enumName, out var enumType))
            return false;
        return enumType.Members.TryGetValue(memberName, out value);
    }

    private static readonly HashSet<string> ValidBackingTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "uint32", "int32", "uint64", "int64"
    };

    public static bool IsValidBackingType(string typeName)
        => ValidBackingTypes.Contains(typeName);

    public bool Register(EnumBlockNode block, CompileTimeConstTable constTable, ConstExpressionEvaluator evaluator,
        out EnumTypeSymbol enumType, out Diagnostic? error, Dictionary<string, EnumTypeSymbol>? scopeTarget = null)
    {
        error = null;
        enumType = null!;

        var target = scopeTarget ?? _enums;
        if (target.ContainsKey(block.Name) || (scopeTarget == null && _enums.ContainsKey(block.Name)))
        {
            error = new Diagnostic("HLAX0039", DiagnosticSeverity.Error,
                $"Duplicate enum type '{block.Name}'", block.Line, block.Column);
            return false;
        }

        if (scopeTarget != null && _enums.ContainsKey(block.Name))
        {
            error = new Diagnostic("HLAX0039", DiagnosticSeverity.Error,
                $"Enum type '{block.Name}' conflicts with a program-level enum", block.Line, block.Column);
            return false;
        }

        var backing = TypeRegistry.Lookup(block.BackingType);
        if (backing == null || !IsValidBackingType(block.BackingType))
        {
            error = new Diagnostic("HLAX0040", DiagnosticSeverity.Error,
                $"Enum backing type must be uint32, int32, uint64, or int64, not '{block.BackingType}'",
                block.Line, block.Column);
            return false;
        }

        var members = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        long nextValue = 0;
        foreach (var member in block.Members)
        {
            if (!seen.Add(member.Name))
            {
                error = new Diagnostic("HLAX0039", DiagnosticSeverity.Error,
                    $"Duplicate enum member '{member.Name}' in enum '{block.Name}'",
                    member.Line, member.Column);
                return false;
            }

            long value;
            if (member.Value != null)
            {
                if (!evaluator.TryEvaluate(member.Value, constTable, out value, out error))
                    return false;
                nextValue = value + 1;
            }
            else
            {
                value = nextValue;
                nextValue++;
            }

            members[member.Name] = value;
            constTable.Define(QualifiedName(block.Name, member.Name), value);
        }

        enumType = new EnumTypeSymbol
        {
            Name = block.Name,
            BackingType = backing,
            Members = members
        };
        target[block.Name] = enumType;
        if (scopeTarget == null)
            _enums[block.Name] = enumType;
        return true;
    }

    public bool TryGetScoped(string name, IReadOnlyDictionary<string, EnumTypeSymbol>? scope, out EnumTypeSymbol enumType)
    {
        if (scope != null && scope.TryGetValue(name, out enumType!))
            return true;
        return _enums.TryGetValue(name, out enumType!);
    }

    public static string QualifiedName(string enumName, string memberName)
        => $"{enumName}.{memberName}";
}
