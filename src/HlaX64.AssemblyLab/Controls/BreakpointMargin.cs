using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;

namespace HlaX64.AssemblyLab.Controls;

/// <summary>
/// Clickable left margin for toggling source breakpoints (Visual Studio style).
/// </summary>
public sealed class BreakpointMargin : AbstractMargin
{
    private readonly IBrush _backgroundBrush = new ImmutableSolidColorBrush(new Color(255, 51, 51, 51));
    private readonly IBrush _pointerOverBrush = new ImmutableSolidColorBrush(new Color(192, 80, 80, 80));
    private readonly IPen _pointerOverPen = new ImmutablePen(new ImmutableSolidColorBrush(new Color(192, 37, 37, 37)), 1);
    private readonly IBrush _markerBrush = new ImmutableSolidColorBrush(new Color(255, 195, 81, 92));
    private readonly IPen _markerPen = new ImmutablePen(new ImmutableSolidColorBrush(new Color(255, 240, 92, 104)), 1);

    private readonly HashSet<int> _markedLines = [];
    private int _pointerOverLine = -1;

    public event Action<int>? BreakpointToggled;

    public BreakpointMargin()
    {
        Cursor = new Cursor(StandardCursorType.Arrow);
    }

    public void SetBreakpoints(IEnumerable<int> lines)
    {
        _markedLines.Clear();
        foreach (var line in lines)
            if (line > 0)
                _markedLines.Add(line);
        InvalidateVisual();
    }

    protected override void OnTextViewChanged(TextView? oldTextView, TextView? newTextView)
    {
        if (oldTextView != null)
        {
            oldTextView.VisualLinesChanged -= OnVisualLinesChanged;
            oldTextView.DocumentChanged -= OnDocumentChanged;
        }

        if (newTextView != null)
        {
            newTextView.VisualLinesChanged += OnVisualLinesChanged;
            newTextView.DocumentChanged += OnDocumentChanged;
        }

        base.OnTextViewChanged(oldTextView, newTextView);
    }

    private void OnVisualLinesChanged(object? sender, EventArgs e) => InvalidateVisual();

    private void OnDocumentChanged(object? sender, DocumentChangedEventArgs e)
    {
        _markedLines.Clear();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) => new(16, 0);

    private int GetLineNumber(PointerEventArgs e)
    {
        if (TextView == null)
            return -1;

        double visualY = e.GetPosition(TextView).Y + TextView.VerticalOffset;
        VisualLine? visualLine = TextView.GetVisualLineFromVisualTop(visualY);
        return visualLine == null ? -1 : visualLine.FirstDocumentLine.LineNumber;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        _pointerOverLine = GetLineNumber(e);
        InvalidateVisual();
        base.OnPointerMoved(e);
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        _pointerOverLine = -1;
        InvalidateVisual();
        base.OnPointerExited(e);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        int line = _pointerOverLine = GetLineNumber(e);
        if (line <= 0)
        {
            base.OnPointerPressed(e);
            return;
        }

        BreakpointToggled?.Invoke(line);
        e.Handled = true;
        base.OnPointerPressed(e);
    }

    public override void Render(DrawingContext context)
    {
        context.DrawRectangle(_backgroundBrush, null, Bounds);

        if (TextView?.VisualLinesValid != true)
        {
            base.Render(context);
            return;
        }

        foreach (var visualLine in TextView.VisualLines)
        {
            int lineNumber = visualLine.FirstDocumentLine.LineNumber;
            double y = visualLine.VisualTop - TextView.VerticalOffset + visualLine.Height / 2;

            if (_markedLines.Contains(lineNumber))
                context.DrawEllipse(_markerBrush, _markerPen, new Point(8, y), 6, 6);
            else if (_pointerOverLine == lineNumber)
                context.DrawEllipse(_pointerOverBrush, _pointerOverPen, new Point(8, y), 6, 6);
        }

        base.Render(context);
    }
}
