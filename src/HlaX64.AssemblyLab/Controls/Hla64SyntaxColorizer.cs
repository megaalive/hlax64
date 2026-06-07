using System.Text.RegularExpressions;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace HlaX64.AssemblyLab.Controls;

/// <summary>Lightweight syntax coloring for .hla64 — no TextMate flicker on Windows.</summary>
public sealed partial class Hla64SyntaxColorizer : DocumentColorizingTransformer
{
    private static readonly IBrush KeywordBrush = new SolidColorBrush(Color.Parse("#569CD6"));
    private static readonly IBrush StringBrush = new SolidColorBrush(Color.Parse("#CE9178"));
    private static readonly IBrush CommentBrush = new SolidColorBrush(Color.Parse("#6A9955"));
    private static readonly IBrush DirectiveBrush = new SolidColorBrush(Color.Parse("#C586C0"));
    private static readonly IBrush NumberBrush = new SolidColorBrush(Color.Parse("#B5CEA8"));

    [GeneratedRegex(@"\b(program|begin|end|procedure|proc|return|if|then|else|elseif|while|for|repeat|until|break|continue|const|static|readonly|record|union|enum|namespace|include|extern|forward|returns|call|true|false|not|and|or|xor|shl|shr|mod|try|catch|finally|raise|asm|endp|endc|endr|endu|endt|endn|endf|ends|endw|endl|endtry|endasm)\b", RegexOptions.IgnoreCase)]
    private static partial Regex KeywordPattern();

    [GeneratedRegex(@"""([^""\\]|\\.)*""")]
    private static partial Regex StringPattern();

    [GeneratedRegex(@"//.*$|;\s*.*$")]
    private static partial Regex CommentPattern();

    [GeneratedRegex(@"#include\s*\([^)]+\)|#\w+")]
    private static partial Regex DirectivePattern();

    [GeneratedRegex(@"\b\d+\b")]
    private static partial Regex NumberPattern();

    protected override void ColorizeLine(DocumentLine line)
    {
        var text = CurrentContext.Document.GetText(line);
        if (text.Length == 0)
            return;

        foreach (Match m in CommentPattern().Matches(text))
            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length, element => element.TextRunProperties.SetForegroundBrush(CommentBrush));

        foreach (Match m in StringPattern().Matches(text))
            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length, element => element.TextRunProperties.SetForegroundBrush(StringBrush));

        foreach (Match m in DirectivePattern().Matches(text))
            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length, element => element.TextRunProperties.SetForegroundBrush(DirectiveBrush));

        foreach (Match m in KeywordPattern().Matches(text))
            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length, element => element.TextRunProperties.SetForegroundBrush(KeywordBrush));

        foreach (Match m in NumberPattern().Matches(text))
            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length, element => element.TextRunProperties.SetForegroundBrush(NumberBrush));
    }
}
