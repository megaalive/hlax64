using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using HlaX64.AssemblyLab.Controls;

namespace HlaX64.AssemblyLab.Services;

public static class SourceEditorSetup
{
    public const string GrammarScope = "source.hla64";

    public static BreakpointMargin Configure(TextEditor editor, Action<int>? onBreakpointToggled = null)
    {
        editor.ShowLineNumbers = true;
        editor.FontFamily = "Consolas,Courier New,monospace";
        editor.WordWrap = true;

        var margin = new BreakpointMargin();
        if (onBreakpointToggled != null)
            margin.BreakpointToggled += onBreakpointToggled;
        editor.TextArea.LeftMargins.Insert(0, margin);

        var registryOptions = new Hla64RegistryOptions();
        TextMate.Installation installation = editor.InstallTextMate(registryOptions);
        installation.SetGrammar(GrammarScope);

        return margin;
    }
}
