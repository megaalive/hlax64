namespace HlaX64.Compiler.Debug;

public sealed class SourceMapEntry
{
    public int SourceLine { get; init; }
    public int? SourceColumn { get; init; }
    public int? EndLine { get; init; }
    public int? EndColumn { get; init; }
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

    /// <summary>
    /// Emit VAA plan §13 <c>candidate.map.json</c> (schema 0.1).
    /// Uses HlaX64 pack key aliases (<c>hla_source</c>, <c>compiler_source</c>,
    /// <c>compiler_revision</c>) that VAA's source-map join accepts.
    /// </summary>
    public string ToVaaCandidateMapJson()
    {
        var sourceName = string.IsNullOrEmpty(Source)
            ? "input.hla64"
            : Path.GetFileName(Source);

        var entries = new List<object>();
        foreach (var e in Entries)
        {
            if (e.NasmLine is null)
                continue;

            var hla = e.SourceColumn is int col
                ? $"{sourceName}:{e.SourceLine}:{col}"
                : $"{sourceName}:{e.SourceLine}";
            var ir = string.IsNullOrEmpty(e.IrOpcode)
                ? $"Ir#{e.IrId}"
                : $"{e.IrOpcode}#{e.IrId}";

            entries.Add(new Dictionary<string, object?>
            {
                ["assembly_line"] = (long)e.NasmLine.Value,
                ["hla_source"] = hla,
                ["ir_node"] = ir,
            });
        }

        var doc = new Dictionary<string, object?>
        {
            ["schema_version"] = "0.1",
            ["compiler_revision"] = string.IsNullOrEmpty(CompilerVersion)
                ? "hlax64:unknown"
                : CompilerVersion.StartsWith("git:", StringComparison.Ordinal)
                    ? CompilerVersion
                    : $"hlax64:{CompilerVersion}",
            ["entries"] = entries,
        };

        return System.Text.Json.JsonSerializer.Serialize(doc, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        });
    }

    public SourceMapEntry? LookupBySource(int line, int? column = null)
    {
        SourceMapEntry? best = null;
        foreach (var e in Entries)
        {
            if (e.SourceLine != line) continue;
            if (column == null || e.SourceColumn == null || e.SourceColumn == column)
                return e;
            best ??= e;
        }
        return best;
    }

    public SourceMapEntry? LookupByNasmLine(int nasmLine)
        => Entries.FirstOrDefault(e => e.NasmLine == nasmLine);

    public SourceMapEntry? LookupByIrId(int irId)
        => Entries.FirstOrDefault(e => e.IrId == irId);
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
                        EndLine = inst.SourceLine,
                        EndColumn = inst.SourceColumn.HasValue ? inst.SourceColumn + 1 : null,
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
            EndLine = e.EndLine,
            EndColumn = e.EndColumn,
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
