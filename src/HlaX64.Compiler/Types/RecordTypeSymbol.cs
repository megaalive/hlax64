namespace HlaX64.Compiler.Types;

public sealed class RecordFieldSymbol
{
    public required string Name { get; init; }
    public required IntegerTypeSymbol Type { get; init; }
    public required int Offset { get; init; }
    public int SizeBytes => Type.BitWidth / 8;
}

public sealed class RecordTypeSymbol
{
    public string Name { get; }
    public IReadOnlyList<RecordFieldSymbol> Fields { get; }
    public int SizeInBytes { get; }
    public int Alignment { get; }

    private readonly Dictionary<string, RecordFieldSymbol> _fieldsByName;

    public RecordTypeSymbol(string name, IReadOnlyList<RecordFieldSymbol> fields, int sizeInBytes, int alignment)
    {
        Name = name;
        Fields = fields;
        SizeInBytes = sizeInBytes;
        Alignment = alignment;
        _fieldsByName = fields.ToDictionary(f => f.Name, f => f, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetField(string name, out RecordFieldSymbol field)
        => _fieldsByName.TryGetValue(name, out field!);
}
