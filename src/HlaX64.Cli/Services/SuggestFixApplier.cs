using System.Text.Json;
using System.Text.RegularExpressions;
using HlaX64.Compiler.Diagnostics;

namespace HlaX64.Cli.Services;

public sealed record SuggestFixApplyResult(bool Success, string PatchedSource, string Message);

/// <summary>Applies structured suggestedFix payloads from explain/agent JSON to source text.</summary>
public static partial class SuggestFixApplier
{
    public static SuggestFixApplyResult TryApplyFirstFromAgentJson(string sourceText, string agentJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(agentJson);
            if (!doc.RootElement.TryGetProperty("diagnostics", out var diags))
                return Fail(sourceText, "No diagnostics in agent report.");

            foreach (var d in diags.EnumerateArray())
            {
                if (!d.TryGetProperty("suggestedFix", out var fix) || fix.ValueKind == JsonValueKind.Null)
                    continue;

                var line = d.TryGetProperty("line", out var ln) ? ln.GetInt32() : 0;
                var column = d.TryGetProperty("column", out var col) ? col.GetInt32() : 1;
                var result = TryApplyFix(sourceText, fix, line, column);
                if (result.Success)
                    return result;
            }

            return Fail(sourceText, "No applicable suggestedFix found (need replacement or replaceToken).");
        }
        catch (Exception ex)
        {
            return Fail(sourceText, ex.Message);
        }
    }

    public static SuggestFixApplyResult TryApplyFromDiagnostic(string sourceText, Diagnostic diagnostic)
    {
        var fix = ExplainAgentService.SuggestFix(diagnostic);
        if (fix == null)
            return Fail(sourceText, "Diagnostic has no suggested fix.");

        var json = JsonSerializer.Serialize(fix);
        using var doc = JsonDocument.Parse(json);
        return TryApplyFix(sourceText, doc.RootElement, diagnostic.Line, diagnostic.Column);
    }

    public static SuggestFixApplyResult TryApplyFix(string sourceText, JsonElement fix, int line, int column)
    {
        if (fix.TryGetProperty("replacement", out var replEl) && replEl.ValueKind == JsonValueKind.String)
        {
            var replacement = replEl.GetString() ?? "";
            if (string.IsNullOrEmpty(replacement))
                return Fail(sourceText, "Empty replacement.");

            if (line > 0 && TryReplaceTokenAt(sourceText, line, column, replacement, out var patchedAt))
                return new SuggestFixApplyResult(true, patchedAt, $"Applied replacement at L{line} C{column}: '{replacement}'");

            if (TryReplaceFirstToken(sourceText, replacement, out var wrongToken, out var patchedSearch))
                return new SuggestFixApplyResult(true, patchedSearch, $"Applied replacement '{wrongToken}' → '{replacement}'");

            return Fail(sourceText, line > 0
                ? $"Could not locate token at L{line} C{column}."
                : "Could not locate token to replace.");
        }

        if (fix.TryGetProperty("oldText", out var oldEl) &&
            fix.TryGetProperty("newText", out var newEl) &&
            oldEl.ValueKind == JsonValueKind.String &&
            newEl.ValueKind == JsonValueKind.String)
        {
            var oldText = oldEl.GetString() ?? "";
            var newText = newEl.GetString() ?? "";
            if (!sourceText.Contains(oldText, StringComparison.Ordinal))
                return Fail(sourceText, "oldText not found in source.");

            return new SuggestFixApplyResult(
                true,
                sourceText.Replace(oldText, newText, StringComparison.Ordinal),
                "Applied oldText → newText patch.");
        }

        var template = fix.TryGetProperty("template", out var t) ? t.GetString() : null;
        return Fail(sourceText, template != null
            ? $"Fix is advisory only: {template}"
            : "Fix has no replacement fields.");
    }

    public static bool TryReplaceTokenAt(string sourceText, int line, int column, string replacement, out string patched)
    {
        patched = sourceText;
        if (line < 1 || column < 1)
            return false;

        var lines = sourceText.Split('\n');
        if (line > lines.Length)
            return false;

        var row = lines[line - 1].TrimEnd('\r');
        var colIndex = column - 1;
        if (colIndex >= row.Length)
            colIndex = row.Length > 0 ? row.Length - 1 : 0;

        var start = colIndex;
        while (start > 0 && IsTokenChar(row[start - 1]))
            start--;
        var end = colIndex;
        while (end < row.Length && IsTokenChar(row[end]))
            end++;

        if (start == end)
            return false;

        lines[line - 1] = row[..start] + replacement + row[end..];
        patched = string.Join('\n', lines);
        return true;
    }

    private static bool IsTokenChar(char c) =>
        char.IsLetterOrDigit(c) || c is '_' or '@';

    private static bool TryReplaceFirstToken(string sourceText, string replacement, out string wrongToken, out string patched)
    {
        wrongToken = "";
        patched = sourceText;
        var db = HlaX64.Compiler.Cpu.InstructionDatabase.LoadDefault();
        foreach (var line in sourceText.Split('\n'))
        {
            var row = line.TrimEnd('\r');
            foreach (Match m in TokenRegex().Matches(row))
            {
                var token = m.Value;
                if (db.TryGet(token.ToLowerInvariant(), out _))
                    continue;
                var suggestion = db.SuggestClosest(token.ToLowerInvariant());
                if (!string.Equals(suggestion, replacement, StringComparison.OrdinalIgnoreCase))
                    continue;
                wrongToken = token;
                patched = sourceText.Replace(token, replacement, StringComparison.Ordinal);
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"\b[A-Za-z_][A-Za-z0-9_]*\b")]
    private static partial Regex TokenRegex();

    private static SuggestFixApplyResult Fail(string source, string message)
        => new(false, source, message);
}
