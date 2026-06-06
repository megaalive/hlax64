using HlaX64.Compiler.Abi;
using System.Text;

namespace HlaX64.Backend.Nasm.Emitters;

public sealed class NasmEmitter
{
    private readonly StringBuilder _sb = new();

    public string Emit(IReadOnlyList<LoweredFunction> functions, IReadOnlyList<StringLiteralInfo> stringLiterals)
    {
        _sb.Clear();

        _sb.AppendLine("bits 64");

        // Extern declarations for runtime library functions
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
            {
                EmitFunction(func, includePrologue: true);
            }
        }

        _sb.AppendLine();
        if (hasEntry)
        {
            _sb.AppendLine("section .data");
            _sb.AppendLine("newline db 0x0A");
        }
        else
        {
            _sb.AppendLine("section .data");
        }

        foreach (var sl in stringLiterals)
        {
            _sb.AppendLine($"{sl.Label} db \"{EscapeString(sl.Value)}\", 0");
        }

        return _sb.ToString();
    }

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

                // Multi-line instructions (from call lowering etc.)
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