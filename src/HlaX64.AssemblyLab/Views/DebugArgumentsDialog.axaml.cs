using Avalonia.Controls;
using Avalonia.Interactivity;
using HlaX64.AssemblyLab.Models;

namespace HlaX64.AssemblyLab.Views;

public partial class DebugArgumentsDialog : Window
{
    public DebugArgumentsDialog()
    {
        InitializeComponent();
    }

    public static async Task<string?> ShowAsync(Window owner, DebugArgumentsRequest request)
    {
        var dialog = new DebugArgumentsDialog();
        dialog.ProgramText.Text = $"Program: {request.ProgramName}";
        dialog.HintText.Text = request.Hint;
        dialog.ArgumentsBox.Text = request.DefaultArguments;
        dialog.ArgumentsBox.CaretIndex = dialog.ArgumentsBox.Text?.Length ?? 0;
        return await dialog.ShowDialog<string?>(owner);
    }

    private void OnOkClick(object? sender, RoutedEventArgs e)
        => Close(ArgumentsBox.Text ?? string.Empty);

    private void OnCancelClick(object? sender, RoutedEventArgs e)
        => Close(null);
}
