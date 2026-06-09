using Avalonia.Controls;
using Avalonia.Interactivity;
using AvaloniaEdit;
using HlaX64.AssemblyLab.Controls;
using HlaX64.AssemblyLab.Models;
using HlaX64.AssemblyLab.Services;
using HlaX64.AssemblyLab.ViewModels;

namespace HlaX64.AssemblyLab.Views;

public partial class MainWindow : Window
{
    private const int TerminalBottomTabIndex = 1;

    private TextEditor? _sourceEditor;
    private TextEditor? _irEditor;
    private TextEditor? _nasmEditor;
    private TextEditor? _abiEditor;
    private TextEditor? _disasmEditor;
    private BreakpointMargin? _breakpointMargin;
    private bool _syncingSource;
    private bool _syncingIr;
    private bool _syncingNasm;
    private bool _syncingAbi;
    private bool _syncingDisasm;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm)
            return;

        vm.StorageProvider = StorageProvider;

        var labTerminal = this.FindControl<Controls.LabTerminalControl>("LabTerminal");
        var terminalHostPanel = this.FindControl<DockPanel>("TerminalHostPanel");
        if (labTerminal != null)
            vm.AttachTerminalHost(labTerminal);

        var bottomTabs = this.FindControl<TabControl>("BottomTabControl");
        if (bottomTabs != null)
        {
            void SyncTerminalHost()
            {
                var showTerminal = bottomTabs.SelectedIndex == TerminalBottomTabIndex;
                if (terminalHostPanel != null)
                    terminalHostPanel.IsVisible = showTerminal;

                if (showTerminal)
                    labTerminal?.FocusTerminal();
                else
                    labTerminal?.NotifyTabHidden();
            }

            bottomTabs.SelectionChanged += (_, _) => SyncTerminalHost();
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(MainWindowViewModel.SelectedBottomTabIndex))
                    SyncTerminalHost();
            };

            SyncTerminalHost();
        }

        _sourceEditor = this.FindControl<TextEditor>("SourceEditor");
        _irEditor = this.FindControl<TextEditor>("IrEditor");
        _nasmEditor = this.FindControl<TextEditor>("NasmEditor");
        _abiEditor = this.FindControl<TextEditor>("AbiEditor");
        _disasmEditor = this.FindControl<TextEditor>("DisasmEditor");
        if (_sourceEditor == null)
            return;

        if (_irEditor != null)
            PipelineEditorSetup.ConfigureReadOnly(_irEditor, PipelineViewKind.Ir);
        if (_nasmEditor != null)
            PipelineEditorSetup.ConfigureReadOnly(_nasmEditor, PipelineViewKind.Nasm);
        if (_abiEditor != null)
            PipelineEditorSetup.ConfigureReadOnly(_abiEditor, PipelineViewKind.Abi);
        if (_disasmEditor != null)
            PipelineEditorSetup.ConfigureReadOnly(_disasmEditor, PipelineViewKind.Disasm);

        _breakpointMargin = SourceEditorSetup.Configure(_sourceEditor, line =>
        {
            vm.ToggleBreakpoint(line);
            _breakpointMargin?.SetBreakpoints(vm.BreakpointLines);
        });

        _sourceEditor.Text = vm.SourceText;
        SyncPipelineText(_irEditor, vm.IrText, ref _syncingIr);
        SyncPipelineText(_nasmEditor, vm.NasmText, ref _syncingNasm);
        SyncPipelineText(_abiEditor, vm.AbiText, ref _syncingAbi);
        SyncPipelineText(_disasmEditor, vm.DisasmText, ref _syncingDisasm);

        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.WindowTitle))
                Title = vm.WindowTitle;

            if (args.PropertyName == nameof(MainWindowViewModel.SourceText) && _sourceEditor != null)
            {
                if (_syncingSource || _sourceEditor.Text == vm.SourceText)
                    return;

                _syncingSource = true;
                _sourceEditor.Text = vm.SourceText;
                _syncingSource = false;
            }

            if (args.PropertyName == nameof(MainWindowViewModel.IrText))
                SyncPipelineText(_irEditor, vm.IrText, ref _syncingIr);
            if (args.PropertyName == nameof(MainWindowViewModel.NasmText))
                SyncPipelineText(_nasmEditor, vm.NasmText, ref _syncingNasm);
            if (args.PropertyName == nameof(MainWindowViewModel.AbiText))
                SyncPipelineText(_abiEditor, vm.AbiText, ref _syncingAbi);
            if (args.PropertyName == nameof(MainWindowViewModel.DisasmText))
                SyncPipelineText(_disasmEditor, vm.DisasmText, ref _syncingDisasm);
            if (args.PropertyName == nameof(MainWindowViewModel.CurrentDebugLine))
            {
                _breakpointMargin?.SetCurrentLine(vm.CurrentDebugLine);
                ScrollSourceToLine(vm.CurrentDebugLine);
            }
            if (args.PropertyName == nameof(MainWindowViewModel.HighlightedNasmLine))
                ScrollPipelineToLine(_nasmEditor, vm.HighlightedNasmLine);
        };

        Title = vm.WindowTitle;

        _sourceEditor.KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.S && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            {
                vm.SaveFileCommand.Execute(null);
                e.Handled = true;
            }
        };

        _sourceEditor.TextChanged += (_, _) =>
        {
            if (_syncingSource || DataContext is not MainWindowViewModel model)
                return;
            if (model.SourceText == _sourceEditor.Text)
                return;

            _syncingSource = true;
            model.SourceText = _sourceEditor.Text;
            _syncingSource = false;
        };
    }

    private static void SyncPipelineText(TextEditor? editor, string text, ref bool syncing)
    {
        if (editor == null || syncing || editor.Text == text)
            return;

        syncing = true;
        editor.Text = text;
        syncing = false;
    }

    private void ScrollSourceToLine(int line)
    {
        ScrollPipelineToLine(_sourceEditor, line);
    }

    private static void ScrollPipelineToLine(TextEditor? editor, int line)
    {
        if (editor == null || line <= 0 || line > editor.Document.LineCount)
            return;

        editor.TextArea.Caret.Line = line;
        editor.TextArea.Caret.Column = 0;
        editor.ScrollToLine(Math.Max(1, line - 3));
    }

    private void Diagnostics_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        if (sender is ListBox { SelectedItem: LabDiagnosticItem item } &&
            DataContext is MainWindowViewModel vm)
        {
            vm.GoToDiagnosticCommand.Execute(item);
        }
    }
}
