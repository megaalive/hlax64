using AvaloniaTerminal;
using HlaX64.AssemblyLab.Services;

namespace HlaX64.AssemblyLab.Tests;

public class TerminalCursorHelperTests
{
    [Fact]
    public void TrySyncIfStale_Moves_Cursor_When_Engine_Position_Is_Wrong()
    {
        var model = CreateModel();
        model.Feed("D:\\build>dir\r\n");
        model.Feed(" Volume in drive D is Data\r\n");
        model.Feed(" Directory of D:\\build\r\n\r\n");
        model.Feed("01/01/2026  12:00 AM    <DIR>          .\r\n");
        model.Feed("D:\\build>");

        model.Terminal.Buffer.SetCursor(0, 0);

        Assert.True(TerminalCursorHelper.TrySyncIfStale(model));
        Assert.True(TerminalCursorHelper.TryResolvePromptViewportPosition(model, out var column, out var row));
        Assert.Equal(row, model.CaretRow);
        Assert.Equal(column, model.CaretColumn);
        Assert.InRange(column, 8, 10);
        Assert.NotEqual(0, row);
    }

    [Fact]
    public void TrySyncIfStale_Skips_When_Cursor_Already_Aligned()
    {
        var model = CreateModel();
        model.Feed("D:\\build>");

        Assert.False(TerminalCursorHelper.TrySyncIfStale(model));
    }

    [Fact]
    public void IsCursorStale_Detects_Misaligned_Cursor_After_MultiLine_Output()
    {
        var model = CreateModel();
        model.Feed("D:\\build>dir\r\n");
        model.Feed("output line\r\n");
        model.Feed("D:\\build>");
        model.Terminal.Buffer.SetCursor(0, 0);

        Assert.True(TerminalCursorHelper.IsCursorStale(model));
    }

    [Fact]
    public void TryResolvePromptViewportPosition_Returns_False_When_Scrolled_Up()
    {
        var model = CreateModel();
        for (var i = 0; i < 40; i++)
            model.Feed($"line-{i:D2}\r\n");

        model.Feed("D:\\build>");
        model.ScrollLines(-5);

        Assert.False(TerminalCursorHelper.TryResolvePromptViewportPosition(model, out _, out _));
    }

    [Fact]
    public void GetLastContentColumn_Ignores_Trailing_Whitespace()
    {
        var model = CreateModel();
        model.Feed("prompt>   ");

        Assert.True(TerminalCursorHelper.TryResolvePromptViewportPosition(model, out var column, out _));
        Assert.Equal(7, column);
    }

    private static TerminalControlModel CreateModel()
        => new(new TerminalOptions
        {
            Cols = 80,
            Rows = 24,
            Scrollback = 200,
            ReflowOnResize = false,
            TermName = "xterm-256color"
        });
}
