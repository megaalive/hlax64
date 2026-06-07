using Avalonia.Controls;
using Avalonia.Interactivity;
using HlaX64.AssemblyLab.Models;
using HlaX64.AssemblyLab.ViewModels;

namespace HlaX64.AssemblyLab.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.StorageProvider = StorageProvider;
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
