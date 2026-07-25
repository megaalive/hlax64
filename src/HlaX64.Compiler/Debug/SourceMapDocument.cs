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

        // Prefer precise `; ir:N` annotations emitted when AnnotateIrIds is on.
        // Do NOT stamp every IR node onto the function label line â€” that made
        // all entries share one assembly_line and defeated VAA map-join.
        for (int i = 0; i < entries.Count; i++)
        {
            var hint = FindNasmLineForIr(nasmLines, entries[i]);
            if (hint != null)
                entries[i] = CopyWithNasm(entries[i], hint.Value.line, hint.Value.label);
        }

        // Fallback: walk lowered instructions in emit order and assign the next
        // real NASM opcode line after each `; ir:N` (or sequential text lines).
        AssignByLoweredOrder(entries, lowered, nasmLines);

        return new SourceMapDocument
        {
            Source = sourcePath,
            CompilerVersion = compilerVersion,
            Entries = entries
        };
    }

    private static void AssignByLoweredOrder(
        List<SourceMapEntry> entries,
        IReadOnlyList<HlaX64.Compiler.Abi.LoweredFunction> lowered,
        string[] nasmLines)
    {
        foreach (var func in lowered)
        {
            int searchFrom = 0;
            // Skip to the function label so we do not match earlier sections.
            for (int i = 0; i < nasmLines.Length; i++)
            {
                if (nasmLines[i].StartsWith($"{func.Name}:", StringComparison.Ordinal))
                {
                    searchFrom = i + 1;
                    break;
                }
            }

            foreach (var inst in func.Blocks.SelectMany(b => b.Instructions))
            {
                if (inst.IrId is not int irId)
                    continue;

                int idx = entries.FindIndex(e => e.IrId == irId && e.Function == func.Name);
                if (idx < 0 || entries[idx].NasmLine != null)
                    continue;

                var line = FindNextOpcodeLine(nasmLines, ref searchFrom, irId);
                if (line != null)
                    entries[idx] = CopyWithNasm(entries[idx], line.Value, func.Name);
            }
        }
    }

    /// <summary>
    /// Advance through NASM lines; if we see <c>; ir:ID</c> bind to the following
    /// opcode line, otherwise take the next non-empty, non-label, non-comment line.
    /// </summary>
    private static int? FindNextOpcodeLine(string[] nasmLines, ref int searchFrom, int irId)
    {
        int? afterAnnotation = null;
        for (int i = searchFrom; i < nasmLines.Length; i++)
        {
            var trimmed = nasmLines[i].Trim();
            if (trimmed.StartsWith(';') && trimmed.Contains($"ir:{irId}", StringComparison.Ordinal))
            {
                afterAnnotation = i + 1;
                continue;
            }

            if (afterAnnotation is int start && i >= start)
            {
                if (IsOpcodeLine(trimmed))
                {
                    searchFrom = i + 1;
                    return i + 1; // 1-based
                }
            }
        }

        // No annotation: next opcode after searchFrom.
        for (int i = searchFrom; i < nasmLines.Length; i++)
        {
            var trimmed = nasmLines[i].Trim();
            if (IsOpcodeLine(trimmed))
            {
                searchFrom = i + 1;
                return i + 1;
            }
        }

        return null;
    }

    private static bool IsOpcodeLine(string trimmed)
        => trimmed.Length > 0
           && !trimmed.StartsWith(';')
           && !trimmed.EndsWith(':')
           && !trimmed.StartsWith("bits ", StringComparison.OrdinalIgnoreCase)
           && !trimmed.StartsWith("default ", StringComparison.OrdinalIgnoreCase)
           && !trimmed.StartsWith("section ", StringComparison.OrdinalIgnoreCase)
           && !trimmed.StartsWith("global ", StringComparison.OrdinalIgnoreCase)
           && !trimmed.StartsWith("extern ", StringComparison.OrdinalIgnoreCase);

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
            {
                // Bind to the following opcode line when present.
                for (int j = i + 1; j < nasmLines.Length; j++)
                {
                    if (IsOpcodeLine(nasmLines[j].Trim()))
                        return (j + 1, entry.Function);
                }
                return (i + 1, entry.Function);
            }
        }
        return null;
    }
}

