using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using HlaX64.AssemblyLab.ViewModels;

[assembly: AvaloniaTestApplication(typeof(HlaX64.AssemblyLab.App))]

namespace HlaX64.AssemblyLab.Tests;

public class MainWindowHeadlessTests
{
    [AvaloniaFact]
    public void MainWindowViewModel_Initializes_Toolchain_And_Disasm()
    {
        var vm = new MainWindowViewModel();

        Assert.Contains("Toolchain", vm.ToolchainText);
        Assert.Contains("WSL:", vm.ToolchainText);
        Assert.True(vm.BuildCommand.CanExecute(null));
    }
}
