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
    private TextEditor? _sourceEditor;
    private BreakpointMargin? _breakpointMargin;
    private bool _syncingSource;

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

        _sourceEditor = this.FindControl<TextEditor>("SourceEditor");
        if (_sourceEditor == null)
            return;

        _breakpointMargin = SourceEditorSetup.Configure(_sourceEditor, line =>
        {
            vm.ToggleBreakpoint(line);
            _breakpointMargin?.SetBreakpoints(vm.BreakpointLines);
        });

        _sourceEditor.Text = vm.SourceText;
        vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(MainWindowViewModel.SourceText) || _sourceEditor == null)
                return;
            if (_syncingSource || _sourceEditor.Text == vm.SourceText)
                return;

            _syncingSource = true;
            _sourceEditor.Text = vm.SourceText;
            _syncingSource = false;
        };

        _sourceEditor.TextChanged += (_, _) =>
        {
            if (_syncingSource || DataContext is not MainWindowViewModel model)
                return;

            _syncingSource = true;
            model.SourceText = _sourceEditor.Text;
            _syncingSource = false;
        };
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
