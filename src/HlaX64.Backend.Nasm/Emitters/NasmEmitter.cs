using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Types;
using System.Text;

namespace HlaX64.Backend.Nasm.Emitters;

public sealed class NasmEmitter
{
    private readonly StringBuilder _sb = new();

    public string Emit(
        IReadOnlyList<LoweredFunction> functions,
        IReadOnlyList<StringLiteralInfo> stringLiterals,
        IReadOnlyList<GlobalDataSymbol>? globalData = null)
    {
        _sb.Clear();
        globalData ??= Array.Empty<GlobalDataSymbol>();

        _sb.AppendLine("bits 64");

        var allExterns = functions.SelectMany(f => f.RequiredExterns).Distinct().ToList();
        foreach (var ext in allExterns)
            _sb.AppendLine($"extern {ext}");

        _sb.AppendLine("section .text");
        _sb.AppendLine("global _start");
        foreach (var func in functions)
            if (func.IsExport)
                _sb.AppendLine($"global {func.Name}");

        bool hasEntry = false;
        foreach (var func in functions)
        {
            if (func.IsEntryPoint)
            {
                hasEntry = true;
                EmitFunction(func, includePrologue: false);
            }
        }

        foreach (var func in functions)
        {
            if (!func.IsEntryPoint)
                EmitFunction(func, includePrologue: true);
        }

        var bssGlobals = globalData.Where(g => g.InBss).ToList();
        if (bssGlobals.Count > 0)
        {
            _sb.AppendLine();
            _sb.AppendLine("section .bss");
            foreach (var g in bssGlobals)
                EmitBssGlobal(g);
        }

        _sb.AppendLine();
        _sb.AppendLine("section .data");
        if (hasEntry)
            _sb.AppendLine("newline db 0x0A");

        foreach (var g in globalData.Where(g => !g.InBss))
            EmitDataGlobal(g);

        foreach (var sl in stringLiterals)
            _sb.AppendLine($"{sl.Label} db \"{EscapeString(sl.Value)}\", 0");

        return _sb.ToString();
    }

    private void EmitDataGlobal(GlobalDataSymbol g)
    {
        if (g.ElementCount > 1)
        {
            _sb.AppendLine($"{g.Name} {RepeatDirective(DataUnit(g.Type), g.ElementCount)} 0");
            return;
        }

        _sb.AppendLine($"{g.Name} {DataUnit(g.Type)} {g.InitialValue ?? 0}");
    }

    private void EmitBssGlobal(GlobalDataSymbol g)
    {
        _sb.AppendLine($"{g.Name} {ResUnit(g.Type)} {g.ElementCount}");
    }

    private static string DataUnit(IntegerTypeSymbol type) => type.BitWidth switch
    {
        8 => "db",
        16 => "dw",
        32 => "dd",
        _ => "dq"
    };

    private static string ResUnit(IntegerTypeSymbol type) => type.BitWidth switch
    {
        8 => "resb",
        16 => "resw",
        32 => "resd",
        _ => "resq"
    };

    private static string RepeatDirective(string unit, int count) => unit switch
    {
        "db" => $"times {count} db",
        "dw" => $"times {count} dw",
        "dd" => $"times {count} dd",
        _ => $"times {count} dq"
    };

    private void EmitFunction(LoweredFunction func, bool includePrologue)
    {
        _sb.AppendLine();
        _sb.AppendLine($"{func.Name}:");

        foreach (var block in func.Blocks)
        {
            if (block.Label != "entry")
                _sb.AppendLine($"{block.Label}:");

            if (!block.Instructions.Any() && block.Label != "entry")
                continue;

            foreach (var inst in block.Instructions)
            {
                var text = inst.AsmText;
                if (string.IsNullOrEmpty(text))
                    continue;

                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.TrimEnd();
                    if (trimmed.Length > 0)
                        _sb.AppendLine(trimmed.StartsWith("    ") ? trimmed : $"    {trimmed}");
                }
            }
        }
    }

    private static string EscapeString(string s)
    {
        return s.Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
    }
}
