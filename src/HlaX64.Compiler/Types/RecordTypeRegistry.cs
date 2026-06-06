using HlaX64.Compiler.Ast;

namespace HlaX64.Compiler.Types;

public sealed class RecordTypeRegistry
{
    private readonly Dictionary<string, RecordTypeSymbol> _records = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RecordTypeSymbol> Records => _records;

    public void Clear() => _records.Clear();

    public bool TryGet(string name, out RecordTypeSymbol record)
        => _records.TryGetValue(name, out record!);

    public bool Contains(string name) => _records.ContainsKey(name);

    public bool Register(RecordBlockNode block, out RecordTypeSymbol record, out string? error)
    {
        error = null;
        if (_records.ContainsKey(block.Name))
        {
            error = $"Duplicate record type '{block.Name}'";
            record = null!;
            return false;
        }

        var fields = new List<RecordFieldSymbol>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int offset = 0;
        int maxAlign = 1;

        foreach (var fieldNode in block.Fields)
        {
            if (!seen.Add(fieldNode.Name))
            {
                error = $"Duplicate field '{fieldNode.Name}' in record '{block.Name}'";
                record = null!;
                return false;
            }

            var type = TypeRegistry.Lookup(fieldNode.Type);
            if (type == null)
            {
                error = $"Unknown field type '{fieldNode.Type}' in record '{block.Name}'";
                record = null!;
                return false;
            }

            var align = type.BitWidth / 8;
            offset = AlignUp(offset, align);
            fields.Add(new RecordFieldSymbol
            {
                Name = fieldNode.Name,
                Type = type,
                Offset = offset
            });
            offset += align;
            maxAlign = Math.Max(maxAlign, align);
        }

        var size = AlignUp(offset, maxAlign);
        record = new RecordTypeSymbol(block.Name, fields, size, maxAlign);
        _records[block.Name] = record;
        return true;
    }

    private static int AlignUp(int value, int alignment)
        => alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;
}
