using AvaloniaEdit;
using HlaX64.AssemblyLab.Controls;

namespace HlaX64.AssemblyLab.Services;

public static class PipelineEditorSetup
{
    public static TextEditor ConfigureReadOnly(TextEditor editor, PipelineViewKind kind)
    {
        editor.IsReadOnly = true;
        editor.ShowLineNumbers = true;
        editor.FontFamily = "Consolas,Courier New,monospace";
        editor.WordWrap = kind is PipelineViewKind.Ir or PipelineViewKind.Abi;

        editor.TextArea.TextView.LineTransformers.Add(kind switch
        {
            PipelineViewKind.Ir => new IrSyntaxColorizer(),
            PipelineViewKind.Nasm => new NasmSyntaxColorizer(),
            PipelineViewKind.Abi => new AbiSyntaxColorizer(),
            PipelineViewKind.Disasm => new DisasmSyntaxColorizer(),
            _ => new NasmSyntaxColorizer()
        });

        return editor;
    }
}

public enum PipelineViewKind
{
    Ir,
    Nasm,
    Abi,
    Disasm
}
