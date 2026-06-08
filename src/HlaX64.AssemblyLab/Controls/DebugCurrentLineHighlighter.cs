using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace HlaX64.AssemblyLab.Controls;

/// <summary>Highlights the current debug line in the source editor (x64dbg-style RIP line).</summary>
public sealed class DebugCurrentLineHighlighter : IBackgroundRenderer
{
    private readonly IBrush _brush = new ImmutableSolidColorBrush(new Color(64, 78, 201, 176));

    public int HighlightedLine { get; set; } = -1;

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (HighlightedLine <= 0 || textView.Document == null)
            return;

        if (!textView.VisualLinesValid)
            return;

        if (HighlightedLine > textView.Document.LineCount)
            return;

        var line = textView.Document.GetLineByNumber(HighlightedLine);
        var segment = new SimpleSegment(line.Offset, line.Length);
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
        {
            drawingContext.DrawRectangle(
                _brush,
                null,
                new Rect(0, rect.Top - textView.VerticalOffset, textView.Bounds.Width, rect.Height));
        }
    }
}
