using AvaloniaTerminal;
using XTerm.Buffer;

namespace HlaX64.AssemblyLab.Services;

/// <summary>
/// cmd.exe often omits ANSI cursor-position updates after multi-line output (e.g. <c>dir</c>).
/// Reposition the xterm buffer cursor locally after output settles — never while the user is typing.
/// </summary>
internal static class TerminalCursorHelper
{
    private const int StaleRowThreshold = 2;

    public static bool TrySyncIfStale(TerminalControlModel model)
    {
        if (!TryResolvePromptViewportPosition(model, out var column, out var row))
            return false;

        var engineRow = model.CaretRow;
        var engineCol = model.CaretColumn;

        if (Math.Abs(engineRow - row) <= 1 && Math.Abs(engineCol - column) <= 1)
            return false;

        var terminal = model.Terminal;
        terminal.Engine.CursorVisible = true;
        terminal.Buffer.SetCursor(column, row);
        model.EnsureCaretIsVisible();
        model.UpdateDisplay();
        return true;
    }

    internal static bool IsCursorStale(TerminalControlModel model)
    {
        if (!TryResolvePromptViewportPosition(model, out var column, out var row))
            return false;

        return Math.Abs(model.CaretRow - row) > StaleRowThreshold
            || Math.Abs(model.CaretColumn - column) > StaleRowThreshold;
    }

    internal static bool TryResolvePromptViewportPosition(TerminalControlModel model, out int column, out int row)
    {
        column = 0;
        row = 0;

        var terminal = model.Terminal;
        var buffer = terminal.Buffer;
        if (terminal.IsAlternateBufferActive || !buffer.IsAtBottom)
            return false;

        if (!TryFindLastContentLine(buffer, out var absoluteLine))
            return false;

        column = GetLastContentColumn(buffer.GetLine(absoluteLine), terminal.Cols);
        row = absoluteLine - buffer.YDisp;
        if (row < 0 || row >= terminal.Rows)
            row = terminal.Rows - 1;

        return true;
    }

    internal static bool TryFindLastContentLine(TerminalBuffer buffer, out int absoluteLine)
    {
        absoluteLine = Math.Max(0, buffer.Lines.Length - 1);
        for (var lineIndex = buffer.Lines.Length - 1; lineIndex >= 0; lineIndex--)
        {
            var line = buffer.GetLine(lineIndex);
            if (line != null && LineHasContent(line))
            {
                absoluteLine = lineIndex;
                return true;
            }
        }

        return buffer.Lines.Length > 0;
    }

    internal static bool LineHasContent(BufferLine line)
    {
        for (var c = 0; c < line.Length; c++)
        {
            var cell = line[c];
            if (cell.Width <= 0)
                continue;

            if (cell.CodePoint != ' ' && cell.CodePoint != 0)
                return true;
        }

        return false;
    }

    internal static int GetLastContentColumn(BufferLine? line, int cols)
    {
        if (line == null)
            return 0;

        var limit = Math.Min(line.Length, Math.Max(cols, 1));
        for (var c = limit - 1; c >= 0; c--)
        {
            var cell = line[c];
            if (cell.Width <= 0)
                continue;

            if (cell.CodePoint != ' ' && cell.CodePoint != 0)
                return c + 1;
        }

        return 0;
    }
}
