using HlaX64.Compiler.Abi;
using HlaX64.Compiler.Options;
using HlaX64.Compiler.Types;
using System.Text;

namespace HlaX64.Backend.Nasm.Emitters;

public sealed class NasmEmitOptions
{
    public bool EmitDebugInfo { get; init; }
    public bool TraceProcedures { get; init; }
    public bool AnnotateIrIds { get; init; }
    public string? SourceFileName { get; init; }
    public bool IsWindowsTarget { get; init; }
}

public sealed class NasmEmitter
{
    private readonly StringBuilder _sb = new();
    private NasmEmitOptions _options = new();
    private int _currentLine;

    public string Emit(
        IReadOnlyList<LoweredFunction> functions,
        IReadOnlyList<StringLiteralInfo> stringLiterals,
        IReadOnlyList<GlobalDataSymbol>? globalData = null,
        NasmEmitOptions? options = null)
    {
        _sb.Clear();
        _options = options ?? new NasmEmitOptions();
        _currentLine = 0;
        globalData ??= Array.Empty<GlobalDataSymbol>();

        AppendLine("bits 64");
        if (_options.IsWindowsTarget)
            AppendLine("default rel");

        var allExterns = functions.SelectMany(f => f.RequiredExterns).Distinct().ToList();
        foreach (var ext in allExterns)
            AppendLine($"extern {ext}");

        if (_options.TraceProcedures && !_options.IsWindowsTarget)
        {
            AppendLine("extern hla64_trace_enter");
            AppendLine("extern hla64_trace_exit");
        }

        AppendLine("section .text");
        AppendLine("global _start");
        foreach (var func in functions)
            if (func.IsExport)
                AppendLine($"global {func.Name}");

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

        if (_options.EmitDebugInfo && !_options.IsWindowsTarget)
        {
            AppendLine("");
            AppendLine("section .debug_line align=1");
            var srcFile = _options.SourceFileName ?? "source.hla64";
            AppendLine($"    ; DWARF line table stub — file: {srcFile}");
            AppendLine("    dd 0                    ; unit_length (placeholder)");
            AppendLine("    dw 1                    ; version");
            AppendLine("    dd 0                    ; header_length");
            AppendLine("    db 1                    ; minimum_instruction_length");
            AppendLine("    db 1                    ; default_is_stmt");
            AppendLine("    db 0                    ; line_base");
            AppendLine("    db 1                    ; line_range");
            AppendLine("    db 0                    ; opcode_base");
            AppendLine($"    db \"{EscapeString(srcFile)}\", 0  ; file table entry");
        }

        var bssGlobals = globalData.Where(g => g.InBss).ToList();
        if (bssGlobals.Count > 0)
        {
            AppendLine("");
            AppendLine("section .bss");
            foreach (var g in bssGlobals)
                EmitBssGlobal(g);
        }

        AppendLine("");
        AppendLine("section .data");
        if (hasEntry)
            AppendLine("newline db 0x0A");

        foreach (var g in globalData.Where(g => !g.InBss))
            EmitDataGlobal(g);

        foreach (var sl in stringLiterals)
            AppendLine($"{sl.Label} db \"{EscapeString(sl.Value)}\", 0");

        return _sb.ToString();
    }

    private void EmitDataGlobal(GlobalDataSymbol g)
    {
        if (g.ElementCount > 1)
        {
            AppendLine($"{g.Name} {RepeatDirective(DataUnit(g.Type), g.ElementCount)} 0");
            return;
        }

        AppendLine($"{g.Name} {DataUnit(g.Type)} {g.InitialValue ?? 0}");
    }

    private void EmitBssGlobal(GlobalDataSymbol g)
    {
        AppendLine($"{g.Name} {ResUnit(g.Type)} {g.ElementCount}");
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
        AppendLine("");
        if (_options.EmitDebugInfo && !_options.IsWindowsTarget && _options.SourceFileName != null)
            AppendLine($"%line 1 {_options.SourceFileName}");

        if (_options.TraceProcedures)
        {
            AppendLine($"    ; @trace-enter {func.Name}");
            if (!_options.IsWindowsTarget)
            {
                AppendLine("    int3    ; trace breakpoint (Linux MVP)");
                AppendLine("    ; call hla64_trace_enter  ; link runtime trace stub when available");
            }
        }

        AppendLine($"{func.Name}:");

        foreach (var block in func.Blocks)
        {
            if (block.Label != "entry")
                AppendLine($"{block.Label}:");

            if (!block.Instructions.Any() && block.Label != "entry")
                continue;

            foreach (var inst in block.Instructions)
            {
                var text = inst.AsmText;
                if (string.IsNullOrEmpty(text))
                    continue;

                if (_options.EmitDebugInfo && inst.SourceLine != null && !_options.IsWindowsTarget && _options.SourceFileName != null)
                    AppendLine($"%line {inst.SourceLine} {_options.SourceFileName}");

                if (_options.AnnotateIrIds && inst.IrId != null)
                    AppendLine($"    ; ir:{inst.IrId}");

                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    var trimmed = line.TrimEnd();
                    if (trimmed.Length > 0)
                        AppendLine(trimmed.StartsWith("    ") ? trimmed : $"    {trimmed}");
                }
            }
        }

        if (_options.TraceProcedures)
        {
            AppendLine($"    ; @trace-exit {func.Name}");
            if (!_options.IsWindowsTarget)
                AppendLine("    ; call hla64_trace_exit");
        }
    }

    private void AppendLine(string line)
    {
        _sb.AppendLine(line);
        _currentLine++;
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
