using HlaX64.Compiler.Ast;

namespace HlaX64.Compiler.Types;

public sealed class RecordTypeRegistry
{
    private readonly Dictionary<string, RecordTypeSymbol> _records = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, RecordTypeSymbol> Records => _records;

    public void Clear() => _records.Clear();

    public bool TryGet(string name, out RecordTypeSymbol record)
        => _records.TryGetValue(name, out record!);

    public bool Contains(string name) => _records.ContainsKey(name) || string.Equals(name, "utf8slice", StringComparison.OrdinalIgnoreCase);

    public void RegisterBuiltins()
    {
        if (_records.ContainsKey("utf8slice"))
            return;

        var fields = new List<RecordFieldSymbol>
        {
            new() { Name = "ptr", Type = TypeRegistry.Ptr, Offset = 0 },
            new() { Name = "len", Type = TypeRegistry.UInt64, Offset = 8 }
        };
        _records["utf8slice"] = new RecordTypeSymbol("utf8slice", fields, 16, 8);
    }

    public bool Register(RecordBlockNode block, out RecordTypeSymbol record, out string? error,
        Dictionary<string, RecordTypeSymbol>? scopeTarget = null)
    {
        error = null;
        var target = scopeTarget ?? _records;
        if (target.ContainsKey(block.Name) || (scopeTarget == null && _records.ContainsKey(block.Name)))
        {
            error = $"Duplicate record type '{block.Name}'";
            record = null!;
            return false;
        }

        if (scopeTarget != null && _records.ContainsKey(block.Name))
        {
            error = $"Record type '{block.Name}' conflicts with a program-level record";
            record = null!;
            return false;
        }

        if (string.Equals(block.Name, "utf8slice", StringComparison.OrdinalIgnoreCase))
        {
            error = "Record name 'utf8slice' is reserved for the built-in string slice type";
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
            if (type == null && !string.Equals(fieldNode.Type, "utf8slice", StringComparison.OrdinalIgnoreCase))
            {
                error = $"Unknown field type '{fieldNode.Type}' in record '{block.Name}'";
                record = null!;
                return false;
            }

            if (type == null && TryGet("utf8slice", out var utf8Rec))
            {
                error = $"Nested utf8slice fields are not supported in record '{block.Name}'";
                record = null!;
                return false;
            }

            var align = type!.BitWidth / 8;
            if (!block.IsPacked)
                offset = AlignUp(offset, align);
            fields.Add(new RecordFieldSymbol
            {
                Name = fieldNode.Name,
                Type = type,
                Offset = offset
            });
            offset += align;
            if (!block.IsPacked)
                maxAlign = Math.Max(maxAlign, align);
        }

        var size = block.IsPacked ? offset : AlignUp(offset, maxAlign);
        record = new RecordTypeSymbol(block.Name, fields, size, block.IsPacked ? 1 : maxAlign);
        target[block.Name] = record;
        if (scopeTarget == null)
            _records[block.Name] = record;
        return true;
    }

    private static int AlignUp(int value, int alignment)
        => alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;
}
