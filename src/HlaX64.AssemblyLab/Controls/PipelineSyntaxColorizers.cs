using System.Text.RegularExpressions;

using Avalonia.Media;

using AvaloniaEdit.Document;

using AvaloniaEdit.Rendering;



namespace HlaX64.AssemblyLab.Controls;



internal static class SyntaxBrushes

{

    internal static readonly IBrush Keyword = new SolidColorBrush(Color.Parse("#569CD6"));

    internal static readonly IBrush String = new SolidColorBrush(Color.Parse("#CE9178"));

    internal static readonly IBrush Comment = new SolidColorBrush(Color.Parse("#6A9955"));

    internal static readonly IBrush Directive = new SolidColorBrush(Color.Parse("#C586C0"));

    internal static readonly IBrush Number = new SolidColorBrush(Color.Parse("#B5CEA8"));

    internal static readonly IBrush Label = new SolidColorBrush(Color.Parse("#DCDCAA"));

    internal static readonly IBrush Register = new SolidColorBrush(Color.Parse("#4FC1FF"));

    internal static readonly IBrush Address = new SolidColorBrush(Color.Parse("#9CDCFE"));

    internal static readonly IBrush Annotation = new SolidColorBrush(Color.Parse("#808080"));

}



internal static partial class X64SyntaxPatterns

{

    [GeneratedRegex(@"\b(?:r(?:ax|bx|cx|dx|si|di|bp|sp|ip)|r(?:8|9|1[0-5]|[0-9]{1,2})(?:d|w|b)?|e(?:ax|bx|cx|dx|si|di|bp|sp|ip)|[abcd][lh]|sil|dil|bpl|spl|xmm\d+|ymm\d+|zmm\d+|rip)\b", RegexOptions.IgnoreCase)]

    internal static partial Regex Register();



    [GeneratedRegex(@"\b(mov|lea|call|ret|push|pop|xor|add|sub|imul|idiv|cmp|test|jmp|je|jne|jl|jg|jle|jge|ja|jb|syscall|and|or|not|shl|shr|sal|sar|nop|int3|movzx|movsx|cqo|div|inc|dec|jbe|jae|jz|jnz|leave|enter|pushf|popf)\b", RegexOptions.IgnoreCase)]

    internal static partial Regex Mnemonic();

}



public sealed partial class NasmSyntaxColorizer : DocumentColorizingTransformer

{

    [GeneratedRegex(@";.*$")]

    private static partial Regex CommentPattern();



    [GeneratedRegex(@"^\s*\w+:")]

    private static partial Regex LabelPattern();



    [GeneratedRegex(@"\b(bits|section|global|extern|default|db|dw|dd|dq|times|equ|align|incbin|struc|endstruc)\b", RegexOptions.IgnoreCase)]

    private static partial Regex DirectivePattern();



    [GeneratedRegex(@"""([^""\\]|\\.)*""")]

    private static partial Regex StringPattern();



    [GeneratedRegex(@"\b\d+(?:h|b|o|d|q)?\b", RegexOptions.IgnoreCase)]

    private static partial Regex NumberPattern();



    protected override void ColorizeLine(DocumentLine line)

    {

        var text = CurrentContext.Document.GetText(line);

        if (text.Length == 0)

            return;



        ApplyMatches(line, CommentPattern(), SyntaxBrushes.Comment);

        ApplyMatches(line, StringPattern(), SyntaxBrushes.String);

        ApplyMatches(line, LabelPattern(), SyntaxBrushes.Label);

        ApplyMatches(line, DirectivePattern(), SyntaxBrushes.Directive);

        ApplyMatches(line, X64SyntaxPatterns.Mnemonic(), SyntaxBrushes.Keyword);

        ApplyMatches(line, X64SyntaxPatterns.Register(), SyntaxBrushes.Register);

        ApplyMatches(line, NumberPattern(), SyntaxBrushes.Number);

    }



    private void ApplyMatches(DocumentLine line, Regex pattern, IBrush brush)

    {

        var text = CurrentContext.Document.GetText(line);

        foreach (Match m in pattern.Matches(text))

            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length,

                element => element.TextRunProperties.SetForegroundBrush(brush));

    }

}



public sealed partial class IrSyntaxColorizer : DocumentColorizingTransformer

{

    [GeneratedRegex(@";.*$|//.*$")]

    private static partial Regex CommentPattern();



    [GeneratedRegex(@"\b(define|block|br|cond|return|call|phi|load|store|add|sub|mul|div|icmp|alloca|global|function|entry)\b", RegexOptions.IgnoreCase)]

    private static partial Regex KeywordPattern();



    [GeneratedRegex(@"%[\w.-]+|@[\w.-]+")]

    private static partial Regex ValuePattern();



    [GeneratedRegex(@"\b(i\d+|ptr|void|label)\b", RegexOptions.IgnoreCase)]

    private static partial Regex TypePattern();



    protected override void ColorizeLine(DocumentLine line)

    {

        var text = CurrentContext.Document.GetText(line);

        if (text.Length == 0)

            return;



        ApplyMatches(line, CommentPattern(), SyntaxBrushes.Comment);

        ApplyMatches(line, TypePattern(), SyntaxBrushes.Directive);

        ApplyMatches(line, ValuePattern(), SyntaxBrushes.Register);

        ApplyMatches(line, KeywordPattern(), SyntaxBrushes.Keyword);

    }



    private void ApplyMatches(DocumentLine line, Regex pattern, IBrush brush)

    {

        var text = CurrentContext.Document.GetText(line);

        foreach (Match m in pattern.Matches(text))

            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length,

                element => element.TextRunProperties.SetForegroundBrush(brush));

    }

}



public sealed partial class AbiSyntaxColorizer : DocumentColorizingTransformer

{

    [GeneratedRegex(@";.*$")]

    private static partial Regex CommentPattern();



    [GeneratedRegex(@"(?i)\b(preserves|clobbers|inputs|returns|stack-align|notes|caller-saved|callee-saved|target|name|function|entry|export|stack frame|parameters|preserved|externs|clobber)\b")]

    private static partial Regex KeywordPattern();



    [GeneratedRegex(@"\b\d+(?:h|b|o|d|q)?\b", RegexOptions.IgnoreCase)]

    private static partial Regex NumberPattern();



    protected override void ColorizeLine(DocumentLine line)

    {

        var text = CurrentContext.Document.GetText(line);

        if (text.Length == 0)

            return;



        ApplyMatches(line, CommentPattern(), SyntaxBrushes.Comment);

        ApplyMatches(line, KeywordPattern(), SyntaxBrushes.Keyword);

        ApplyMatches(line, X64SyntaxPatterns.Mnemonic(), SyntaxBrushes.Directive);

        ApplyMatches(line, X64SyntaxPatterns.Register(), SyntaxBrushes.Register);

        ApplyMatches(line, NumberPattern(), SyntaxBrushes.Number);

    }



    private void ApplyMatches(DocumentLine line, Regex pattern, IBrush brush)

    {

        var text = CurrentContext.Document.GetText(line);

        foreach (Match m in pattern.Matches(text))

            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length,

                element => element.TextRunProperties.SetForegroundBrush(brush));

    }

}



public sealed partial class DisasmSyntaxColorizer : DocumentColorizingTransformer

{

    [GeneratedRegex(@"(?i)^\s*;\s*src:\s*\d+")]

    private static partial Regex SourceMapPattern();



    [GeneratedRegex(@"(?i)\b[0-9a-f]{2,16}:\b")]

    private static partial Regex AddressPattern();



    [GeneratedRegex(@"\b\d+(?:h|b|o|d|q)?\b", RegexOptions.IgnoreCase)]

    private static partial Regex NumberPattern();



    protected override void ColorizeLine(DocumentLine line)

    {

        var text = CurrentContext.Document.GetText(line);

        if (text.Length == 0)

            return;



        ApplyMatches(line, SourceMapPattern(), SyntaxBrushes.Annotation);

        ApplyMatches(line, CommentPattern(), SyntaxBrushes.Comment);

        ApplyMatches(line, AddressPattern(), SyntaxBrushes.Address);

        ApplyMatches(line, X64SyntaxPatterns.Mnemonic(), SyntaxBrushes.Keyword);

        ApplyMatches(line, X64SyntaxPatterns.Register(), SyntaxBrushes.Register);

        ApplyMatches(line, NumberPattern(), SyntaxBrushes.Number);

    }



    [GeneratedRegex(@";.*$")]

    private static partial Regex CommentPattern();



    private void ApplyMatches(DocumentLine line, Regex pattern, IBrush brush)

    {

        var text = CurrentContext.Document.GetText(line);

        foreach (Match m in pattern.Matches(text))

            ChangeLinePart(line.Offset + m.Index, line.Offset + m.Index + m.Length,

                element => element.TextRunProperties.SetForegroundBrush(brush));

    }

}


