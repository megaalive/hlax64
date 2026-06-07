using AvaloniaEdit;
using HlaX64.AssemblyLab.Controls;

namespace HlaX64.AssemblyLab.Services;

public static class SourceEditorSetup
{
    public const string GrammarScope = "source.hla64";

    public static BreakpointMargin Configure(TextEditor editor, Action<int>? onBreakpointToggled = null)
    {
        editor.ShowLineNumbers = true;
        editor.FontFamily = "Consolas,Courier New,monospace";
        editor.WordWrap = false;

        var margin = new BreakpointMargin();
        if (onBreakpointToggled != null)
            margin.BreakpointToggled += onBreakpointToggled;
        editor.TextArea.LeftMargins.Insert(0, margin);

        var currentLineHighlighter = new DebugCurrentLineHighlighter();
        editor.TextArea.TextView.BackgroundRenderers.Add(currentLineHighlighter);

        editor.TextArea.TextView.LineTransformers.Add(new Hla64SyntaxColorizer());

        margin.CurrentLineHighlighter = currentLineHighlighter;
        return margin;
    }
}
