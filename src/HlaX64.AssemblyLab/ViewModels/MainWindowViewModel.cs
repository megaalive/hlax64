using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HlaX64.AssemblyLab.Models;
using HlaX64.AssemblyLab.Services;
using HlaX64.Compiler.Debug;

namespace HlaX64.AssemblyLab.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AssemblyLabBackend _backend = new();
    private readonly DebugSessionHost _debugHost = new();
    private CancellationTokenSource? _debounceCts;
    private SourceMapDocument? _sourceMap;
    private string _rawNasmText = "";
    private string? _lastBuiltOutputFile;
    private string _repoRoot = FindRepoRoot();

    [ObservableProperty] private string _sourceText = "// Open a .hla64 file or folder with hla64.toml\n";
    [ObservableProperty] private string _sourcePath = "(unsaved)";
    [ObservableProperty] private string _projectFolder = "";
    [ObservableProperty] private string _selectedTarget = AssemblyLabBackend.ResolveDefaultTarget();
    [ObservableProperty] private string _irText = "";
    [ObservableProperty] private string _nasmText = "";
    [ObservableProperty] private string _abiText = "";
    [ObservableProperty] private string _outputText = "";
    [ObservableProperty] private string _dapText = "";
    [ObservableProperty] private string _capabilitiesText = "";
    [ObservableProperty] private string _disasmText = "";
    [ObservableProperty] private string _toolchainText = "";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private int _highlightedNasmLine;

    public ObservableCollection<LabDiagnosticItem> Diagnostics { get; } = [];
    public ObservableCollection<int> BreakpointLines { get; } = [];
    public IReadOnlyList<string> TargetChoices => AssemblyLabBackend.TargetChoices;

    public IStorageProvider? StorageProvider { get; set; }

    public MainWindowViewModel()
    {
        _debugHost.MessageReceived += msg =>
            Dispatcher.UIThread.Post(() => DapText += msg + Environment.NewLine);
        RefreshToolchainInfo();
        RefreshDisasmText();
    }

    private void RefreshToolchainInfo()
    {
        var info = LabToolchainService.Detect();
        ToolchainText = LabToolchainService.Summarize(info);
    }

    private void RefreshDisasmText()
    {
        DisasmText = _backend.GetDisasmText(_rawNasmText, _sourceMap, _lastBuiltOutputFile);
    }

    public void ToggleBreakpoint(int line)
    {
        if (line <= 0)
            return;

        if (!BreakpointLines.Remove(line))
            BreakpointLines.Add(line);

        StatusText = BreakpointLines.Contains(line)
            ? $"Breakpoint set at line {line}"
            : $"Breakpoint cleared at line {line}";
    }

    partial void OnSourceTextChanged(string value)
    {
        _ = DebouncedCompileAsync();
        CapabilitiesText = _backend.SummarizeCapabilities(_backend.AnalyzeCapabilities(value));
    }

    partial void OnSelectedTargetChanged(string value) => _ = DebouncedCompileAsync();

    [RelayCommand]
    private async Task OpenFile()
    {
        var storage = StorageProvider;
        if (storage == null) return;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open HlaX64 source",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("HlaX64 source") { Patterns = ["*.hla64"] }
            ]
        });
        var file = files.FirstOrDefault();
        if (file == null) return;
        var path = file.Path.LocalPath;
        SourcePath = path;
        ProjectFolder = Path.GetDirectoryName(path) ?? "";
        SourceText = await File.ReadAllTextAsync(path);
        StatusText = $"Opened {Path.GetFileName(path)}";
    }

    [RelayCommand]
    private async Task OpenFolder()
    {
        var storage = StorageProvider;
        if (storage == null) return;
        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open project folder",
            AllowMultiple = false
        });
        var folder = folders.FirstOrDefault();
        if (folder == null) return;
        ProjectFolder = folder.Path.LocalPath;
        var manifest = _backend.HasProjectManifest(ProjectFolder);
        var hla64 = _backend.FindHla64Files(ProjectFolder).FirstOrDefault();
        if (hla64 == null)
        {
            StatusText = manifest ? "hla64.toml found but no .hla64 files" : "No .hla64 files in folder";
            return;
        }
        SourcePath = hla64;
        SourceText = await File.ReadAllTextAsync(hla64);
        StatusText = manifest
            ? $"Opened project {Path.GetFileName(ProjectFolder)} → {Path.GetFileName(hla64)}"
            : $"Opened {Path.GetFileName(hla64)}";
    }

    [RelayCommand]
    private async Task Build()
    {
        StatusText = "Building…";
        var result = await Task.Run(() => _backend.Build(SourcePath, SourceText, SelectedTarget));
        if (result.NasmText != null)
        {
            _rawNasmText = result.NasmText;
            NasmText = _rawNasmText;
        }
        if (result.SourceMap != null)
            _sourceMap = result.SourceMap;
        else if (result.SourceMapFile != null)
            _sourceMap = _backend.LoadSourceMap(result.SourceMapFile);
        _lastBuiltOutputFile = result.OutputFile;
        RefreshDisasmText();

        AppendOutput(result.Success ? result.Message : $"Build failed: {result.Message}");
        StatusText = result.Success ? "Build OK" : "Build failed";
        SelectedTabIndex = 2;
    }

    [RelayCommand]
    private async Task Run()
    {
        StatusText = "Running…";
        var result = await Task.Run(() => _backend.Run(SourcePath, SourceText, SelectedTarget));
        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(result.Stdout))
            sb.AppendLine(result.Stdout.TrimEnd());
        if (!string.IsNullOrEmpty(result.Stderr))
            sb.AppendLine(result.Stderr.TrimEnd());
        sb.AppendLine(result.Message);
        AppendOutput(sb.ToString());
        StatusText = result.Success ? $"Exit code {result.ExitCode}" : "Run failed";
    }

    [RelayCommand]
    private void Debug()
    {
        DapText = "";
        _debugHost.StartInProcess(msg => DapText += msg + Environment.NewLine);
        _debugHost.SendInitialize();
        AppendOutput("Debug session started (DAP MVP — Linux gdb when launching binary).");
        StatusText = "Debug session active";
    }

    [RelayCommand]
    private async Task ExportProofBundle()
    {
        StatusText = "Exporting proof bundle…";
        var result = await Task.Run(() => _backend.ExportProofBundle(SourcePath, SourceText, SelectedTarget));
        if (result.NasmText != null)
        {
            _rawNasmText = result.NasmText;
            NasmText = _rawNasmText;
        }
        if (result.SourceMap != null)
            _sourceMap = result.SourceMap;
        else if (result.SourceMapFile != null)
            _sourceMap = _backend.LoadSourceMap(result.SourceMapFile);
        _lastBuiltOutputFile = result.OutputFile;
        RefreshDisasmText();

        if (result.Success && result.ProofBundleDir != null)
        {
            AppendOutput(result.Message.Contains("compile-only", StringComparison.OrdinalIgnoreCase)
                ? result.Message
                : $"Proof bundle exported: {result.ProofBundleDir}");
            var capPath = Path.Combine(result.ProofBundleDir, "capabilities.json");
            if (File.Exists(capPath))
                CapabilitiesText = await File.ReadAllTextAsync(capPath);
            StatusText = result.Message.Contains("compile-only", StringComparison.OrdinalIgnoreCase)
                ? "Proof bundle (compile-only)"
                : "Proof bundle exported";
        }
        else
        {
            AppendOutput($"Proof bundle failed: {result.Message}");
            StatusText = "Proof export failed";
        }
    }

    [RelayCommand]
    private void GoToDiagnostic(LabDiagnosticItem? item)
    {
        if (item == null) return;
        NavigateToSourceLine(item.Line);
    }

    [RelayCommand]
    private void NavigateToSourceLine(int line)
    {
        if (line <= 0) return;
        var nasmLine = _backend.FindNasmLineForSource(_sourceMap, line);
        if (nasmLine != null && !string.IsNullOrEmpty(_rawNasmText))
        {
            HighlightedNasmLine = nasmLine.Value;
            NasmText = _backend.HighlightNasmLine(_rawNasmText, nasmLine.Value);
            SelectedTabIndex = 2;
            StatusText = $"Source L{line} → NASM L{nasmLine}";
        }
        else
        {
            StatusText = $"Source line {line} (no NASM mapping yet — build with source map)";
        }
    }

    private async Task DebouncedCompileAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await Task.Delay(400, token);
            if (token.IsCancellationRequested) return;

            var path = SourcePath;
            var text = SourceText;
            var target = SelectedTarget;
            var result = await Task.Run(() => _backend.Compile(path, text, target), token);

            if (token.IsCancellationRequested) return;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Diagnostics.Clear();
                foreach (var d in result.Diagnostics)
                    Diagnostics.Add(d);
                IrText = result.IrText ?? "";
                if (!string.IsNullOrEmpty(result.NasmText))
                {
                    _rawNasmText = result.NasmText;
                    NasmText = _rawNasmText;
                }
                AbiText = result.AbiText ?? "";
                _sourceMap = result.SourceMap;
                RefreshDisasmText();
                StatusText = result.Success
                    ? $"Compile OK · {Diagnostics.Count} diagnostic(s)"
                    : $"Compile failed · {Diagnostics.Count} diagnostic(s)";
            });
        }
        catch (OperationCanceledException) { }
    }

    private void AppendOutput(string text)
    {
        OutputText = string.IsNullOrEmpty(OutputText) ? text : OutputText + Environment.NewLine + text;
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "HlaX64.slnx")))
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
