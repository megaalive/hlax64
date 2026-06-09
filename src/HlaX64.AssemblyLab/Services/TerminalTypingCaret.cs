using System.Text;
using System.Text.RegularExpressions;
using AvaloniaTerminal;
using XTerm.Buffer;

namespace HlaX64.AssemblyLab.Services;

/// <summary>Resolves where the typing caret should appear in the terminal viewport.</summary>
internal static partial class TerminalTypingCaret
{
    private static readonly Regex WindowsPromptPattern = WindowsPrompt();

    /// <summary>Typing caret is only shown when the viewport follows live output.</summary>
    public static bool IsFollowingLiveOutput(TerminalControlModel model)
        => model.Terminal.Buffer.IsAtBottom;

    public static bool TryGetViewportPosition(TerminalControlModel model, out int column, out int row)
    {
        column = 0;
        row = 0;

        var buffer = model.Terminal.Buffer;
        var cols = model.Terminal.Cols;
        var rows = model.Terminal.Rows;
        if (rows <= 0 || cols <= 0 || buffer.Lines.Length == 0)
            return false;

        var searchFrom = Math.Max(0, buffer.Lines.Length - 16);
        for (var lineIndex = buffer.Lines.Length - 1; lineIndex >= searchFrom; lineIndex--)
        {
            var line = buffer.GetLine(lineIndex);
            var text = GetLineText(line, cols);
            var contentColumn = GetLastContentColumn(line);
            if (contentColumn == 0 && string.IsNullOrWhiteSpace(text))
                continue;

            if (!LooksLikeInteractivePrompt(text) && lineIndex != buffer.Lines.Length - 1)
                continue;

            if (TryMapLineToViewport(lineIndex, contentColumn, buffer, model, out column, out row))
                return true;
        }

        if (TryMapLineToViewport(buffer.Y, model.CaretColumn, buffer, model, out column, out row))
            return true;

        return false;
    }

    private static bool TryMapLineToViewport(
        int lineIndex,
        int contentColumn,
        XTerm.Buffer.TerminalBuffer buffer,
        TerminalControlModel model,
        out int column,
        out int row)
    {
        column = Math.Max(contentColumn, 0);
        row = 0;

        var viewportRow = lineIndex - buffer.YDisp;
        if (viewportRow < 0 || viewportRow >= model.Terminal.Rows)
            return false;

        if (buffer.Y == lineIndex)
            column = Math.Max(contentColumn, model.CaretColumn);

        row = viewportRow;
        return true;
    }

    private static int GetLastContentColumn(BufferLine? line)
    {
        if (line == null)
            return 0;

        for (var col = line.Length - 1; col >= 0; col--)
        {
            var cell = line[col];
            if (cell.Width <= 0)
                continue;

            if (cell.CodePoint != ' ' && cell.CodePoint != 0)
                return col + 1;
        }

        return 0;
    }

    private static string GetLineText(BufferLine? line, int cols)
    {
        if (line == null)
            return string.Empty;

        var sb = new StringBuilder(cols);
        for (var col = 0; col < cols && col < line.Length; col++)
        {
            var cell = line[col];
            if (cell.Width <= 0)
                continue;

            if (cell.Content.Length > 0)
                sb.Append(cell.Content);
            else if (cell.CodePoint > 0)
                sb.Append(char.ConvertFromUtf32(cell.CodePoint));
        }

        return sb.ToString().TrimEnd();
    }

    private static bool LooksLikeInteractivePrompt(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (WindowsPromptPattern.IsMatch(text))
            return true;

        if (text.EndsWith('>'))
            return true;

        return text.EndsWith("$ ", StringComparison.Ordinal)
            || text.EndsWith("PS> ", StringComparison.Ordinal)
            || text.EndsWith("PS ", StringComparison.Ordinal);
    }

    [GeneratedRegex(@"^[A-Za-z]:\\.*>")]
    private static partial Regex WindowsPrompt();
}
