namespace HlaX64.Compiler.Debug;

public sealed class SourceMapEntry
{
    public int SourceLine { get; init; }
    public int? SourceColumn { get; init; }
    public int IrId { get; init; }
    public string? IrOpcode { get; init; }
    public string? Function { get; init; }
    public int? NasmLine { get; init; }
    public string? NasmLabel { get; init; }
}

public sealed class SourceMapDocument
{
    public int Version { get; init; } = 1;
    public string Source { get; init; } = "";
    public string CompilerVersion { get; init; } = "";
    public List<SourceMapEntry> Entries { get; init; } = [];

    public string ToJson()
    {
        return System.Text.Json.JsonSerializer.Serialize(this, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
        });
    }
}

public static class SourceMapBuilder
{
    public static SourceMapDocument Build(
        string sourcePath,
        IReadOnlyList<HlaX64.Compiler.Ir.IrFunction> irFunctions,
        IReadOnlyList<HlaX64.Compiler.Abi.LoweredFunction> lowered,
        string nasmCode,
        string compilerVersion)
    {
        var entries = new List<SourceMapEntry>();
        var nasmLines = nasmCode.Split('\n');

        foreach (var func in irFunctions)
        {
            foreach (var block in func.Blocks)
            {
                foreach (var inst in block.Instructions)
                {
                    if (inst.SourceLine == null) continue;
                    entries.Add(new SourceMapEntry
                    {
                        SourceLine = inst.SourceLine.Value,
                        SourceColumn = inst.SourceColumn,
                        IrId = inst.Id,
                        IrOpcode = inst.Opcode.ToString(),
                        Function = func.Name
                    });
                }
            }
        }

        foreach (var func in lowered)
        {
            int nasmLine = 1;
            foreach (var line in nasmLines)
            {
                if (line.StartsWith($"{func.Name}:", StringComparison.Ordinal))
                {
                    for (int i = 0; i < entries.Count; i++)
                    {
                        var e = entries[i];
                        if (e.Function == func.Name && e.NasmLine == null)
                            entries[i] = CopyWithNasm(e, nasmLine + 1, func.Name);
                    }
                    break;
                }
                nasmLine++;
            }
        }

        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].NasmLine != null) continue;
            var hint = FindNasmLineForIr(nasmLines, entries[i]);
            if (hint != null)
                entries[i] = CopyWithNasm(entries[i], hint.Value.line, hint.Value.label);
        }

        return new SourceMapDocument
        {
            Source = sourcePath,
            CompilerVersion = compilerVersion,
            Entries = entries
        };
    }

    private static SourceMapEntry CopyWithNasm(SourceMapEntry e, int line, string? label)
        => new()
        {
            SourceLine = e.SourceLine,
            SourceColumn = e.SourceColumn,
            IrId = e.IrId,
            IrOpcode = e.IrOpcode,
            Function = e.Function,
            NasmLine = line,
            NasmLabel = label
        };

    private static (int line, string? label)? FindNasmLineForIr(string[] nasmLines, SourceMapEntry entry)
    {
        for (int i = 0; i < nasmLines.Length; i++)
        {
            var trimmed = nasmLines[i].TrimEnd();
            if (trimmed.StartsWith(';') && trimmed.Contains($"ir:{entry.IrId}", StringComparison.Ordinal))
                return (i + 1, entry.Function);
        }
        return null;
    }
}
