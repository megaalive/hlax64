using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HlaX64.AssemblyLab.Models;
using HlaX64.AssemblyLab.Services;
using HlaX64.Cli.Project;
using HlaX64.Compiler.Debug;
using HlaX64.DebugAdapter;

namespace HlaX64.AssemblyLab.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly AssemblyLabBackend _backend = new();
    private readonly DebugSessionHost _debugHost = new();
    private readonly McpSessionHost _mcpHost = new();
    private readonly string _repoRoot = FindRepoRoot();

    private CancellationTokenSource? _debounceCts;
    private SourceMapDocument? _sourceMap;
    private string _rawNasmText = "";
    private string? _lastBuiltOutputFile;
    private string _diffBaselineText = "";
    private string _lastAgentJson = "";
    private bool _suppressDirty;

    [ObservableProperty] private string _sourceText = "// Open a .hla64 file or hla64.toml project\n";
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
    [ObservableProperty] private string _agentText = "";
    [ObservableProperty] private string _mcpText = "";
    [ObservableProperty] private string _planText = "";
    [ObservableProperty] private string _diffText = "";
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _openModeText = "Mode: unsaved source";
    [ObservableProperty] private string _buildOutputDir = "(save file first)";
    [ObservableProperty] private string _windowTitle = "HlaX64 Assembly Lab";
    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private int _highlightedNasmLine;
    [ObservableProperty] private bool _strictPlanGate;
    [ObservableProperty] private bool _planApproved = true;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private bool _isDebugPaused;
    [ObservableProperty] private int _currentDebugLine;

    public bool IsDebugSessionActive => _debugHost.IsRunning;

    public bool IsDebugFeatureEnabled => AssemblyLabFeatures.DebugEnabled;

    public string DebugFeatureDisabledHint => AssemblyLabFeatures.DebugDisabledMessage;

    public bool IsMcpFeatureEnabled => AssemblyLabFeatures.McpEnabled;

    public string McpFeatureDisabledHint => AssemblyLabFeatures.McpDisabledMessage;

    public ObservableCollection<LabDiagnosticItem> Diagnostics { get; } = [];
    public ObservableCollection<int> BreakpointLines { get; } = [];
    public IReadOnlyList<string> TargetChoices => AssemblyLabBackend.TargetChoices;

    public IStorageProvider? StorageProvider { get; set; }

    private bool _suppressDebugStopEvents;

    public MainWindowViewModel()
    {
        _debugHost.MessageReceived += msg =>
        {
            Dispatcher.UIThread.Post(() => DapText += msg + Environment.NewLine);
        };

        _debugHost.DebugStopped += info =>
        {
            if (_suppressDebugStopEvents)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                ApplyDebugStopState(info);
                NotifyDebugCommandsChanged();
            });
        };

        _mcpHost.MessageReceived += msg =>
            Dispatcher.UIThread.Post(() => McpText += msg + Environment.NewLine);

        RefreshToolchainInfo();
        RefreshBuildOutputDir();
        RefreshWindowTitle();
        NotifyDebugCommandsChanged();
        NotifyMcpCommandsChanged();
        if (!AssemblyLabFeatures.DebugEnabled)
            StatusText = AssemblyLabFeatures.DebugDisabledMessage;
        _ = DebouncedCompileAsync();
    }

    private bool CanExecuteGatedActions() => !StrictPlanGate || PlanApproved;

    private void ResetPlanApproval()
    {
        if (StrictPlanGate)
            PlanApproved = false;

        BuildCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        ExportProofBundleCommand.NotifyCanExecuteChanged();
    }

    partial void OnStrictPlanGateChanged(bool value)
    {
        BuildCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        ExportProofBundleCommand.NotifyCanExecuteChanged();
        StatusText = value
            ? "Strict plan gate enabled"
            : "Strict plan gate disabled";
    }

    partial void OnPlanApprovedChanged(bool value)
    {
        BuildCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        ExportProofBundleCommand.NotifyCanExecuteChanged();
    }

    partial void OnSourcePathChanged(string value)
    {
        RefreshBuildOutputDir();
        RefreshWindowTitle();
    }

    partial void OnIsDirtyChanged(bool value)
    {
        RefreshWindowTitle();
    }

    partial void OnSourceTextChanged(string value)
    {
        if (!_suppressDirty)
            IsDirty = true;

        RefreshDiffText();
        ResetPlanApproval();
        _ = DebouncedCompileAsync();
    }

    partial void OnSelectedTargetChanged(string value)
    {
        ResetPlanApproval();
        _ = DebouncedCompileAsync();
    }

    public void ToggleBreakpoint(int line)
    {
        if (line <= 0)
            return;

        if (!AssemblyLabFeatures.DebugEnabled)
        {
            StatusText = AssemblyLabFeatures.DebugDisabledMessage;
            return;
        }

        if (!BreakpointLines.Remove(line))
            BreakpointLines.Add(line);

        StatusText = BreakpointLines.Contains(line)
            ? $"Breakpoint set at line {line}"
            : $"Breakpoint cleared at line {line}";
    }

    [RelayCommand]
    private async Task OpenFile()
    {
        var storage = StorageProvider;
        if (storage == null)
            return;

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
        if (file == null)
            return;

        var path = file.Path.LocalPath;
        var text = await File.ReadAllTextAsync(path);
        await LoadEditorStateAsync(path, text, Path.GetDirectoryName(path) ?? "", "Mode: single file");
        PlanApproved = true;
        StatusText = $"Opened {Path.GetFileName(path)}";
    }

    [RelayCommand]
    private async Task OpenProject()
    {
        var storage = StorageProvider;
        if (storage == null)
            return;

        string? manifestPath = null;

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open project manifest (hla64.toml)",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("HlaX64 manifest") { Patterns = ["hla64.toml", "*.toml"] }
            ]
        });
        var pickedFile = files.FirstOrDefault();
        if (pickedFile != null)
        {
            manifestPath = pickedFile.Path.LocalPath;
        }
        else
        {
            var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Pick project folder containing hla64.toml",
                AllowMultiple = false
            });
            var folder = folders.FirstOrDefault();
            if (folder == null)
                return;

            var folderPath = folder.Path.LocalPath;
            var candidate = Path.Combine(folderPath, "hla64.toml");
            if (!File.Exists(candidate))
            {
                StatusText = "Selected folder does not contain hla64.toml";
                return;
            }

            manifestPath = candidate;
        }

        await LoadProjectAsync(manifestPath);
    }

    [RelayCommand]
    private async Task SaveFile()
    {
        if (SourcePath is "(unsaved)" or "")
        {
            await SaveFileAs();
            return;
        }

        await File.WriteAllTextAsync(SourcePath, SourceText);
        _diffBaselineText = SourceText;
        IsDirty = false;
        RefreshDiffText();
        StatusText = $"Saved {Path.GetFileName(SourcePath)}";
    }

    [RelayCommand]
    private async Task SaveFileAs()
    {
        var storage = StorageProvider;
        if (storage == null)
            return;

        var suggestedName = SourcePath is "(unsaved)" or ""
            ? "main.hla64"
            : Path.GetFileName(SourcePath);

        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save HlaX64 source",
            SuggestedFileName = suggestedName,
            FileTypeChoices =
            [
                new FilePickerFileType("HlaX64 source") { Patterns = ["*.hla64"] }
            ]
        });

        if (file == null)
            return;

        var path = file.Path.LocalPath;
        await File.WriteAllTextAsync(path, SourceText);

        SourcePath = path;
        ProjectFolder = Path.GetDirectoryName(path) ?? "";
        OpenModeText = "Mode: single file";
        _diffBaselineText = SourceText;
        IsDirty = false;
        RefreshDiffText();
        StatusText = $"Saved as {Path.GetFileName(path)}";
    }

    [RelayCommand(CanExecute = nameof(CanExecuteGatedActions))]
    private async Task Build()
    {
        if (!EnsureOpenFileForBuild())
            return;

        ReleaseDebugSessionForBuild();
        StatusText = "Building...";
        var result = await Task.Run(() => _backend.Build(SourcePath, SourceText, SelectedTarget));
        ApplyBuildArtifacts(result);
        AppendOutput(FormatBuildResult("Build", result));
        StatusText = result.Success ? "Build OK" : "Build failed";
        SelectedTabIndex = 2;
    }

    [RelayCommand(CanExecute = nameof(CanExecuteGatedActions))]
    private async Task Run()
    {
        if (!EnsureOpenFileForBuild())
            return;

        StatusText = "Running...";
        var build = await Task.Run(() => _backend.Build(SourcePath, SourceText, SelectedTarget));
        ApplyBuildArtifacts(build);
        AppendOutput(FormatBuildResult("Build (for run)", build));
        if (!build.Success || build.OutputFile == null)
        {
            StatusText = "Run aborted (build failed)";
            return;
        }

        var result = await Task.Run(() => _backend.Run(SourcePath, SourceText, SelectedTarget, build.OutputFile));
        var sb = new StringBuilder();
        sb.AppendLine("Run:");
        if (!string.IsNullOrWhiteSpace(result.Stdout))
            sb.AppendLine(result.Stdout.TrimEnd());
        if (!string.IsNullOrWhiteSpace(result.Stderr))
            sb.AppendLine(result.Stderr.TrimEnd());
        sb.AppendLine(result.Message);
        AppendOutput(sb.ToString().TrimEnd());
        StatusText = result.Success ? $"Exit code {result.ExitCode}" : "Run failed";
    }

    [RelayCommand(CanExecute = nameof(CanStartDebug))]
    private async Task Debug()
    {
        if (!AssemblyLabFeatures.DebugEnabled)
        {
            StatusText = AssemblyLabFeatures.DebugDisabledMessage;
            return;
        }

        if (!EnsureOpenFileForBuild())
            return;

        ReleaseDebugSessionForBuild();
        DapText = "";
        StatusText = "Building for debug...";
        var build = await Task.Run(() => _backend.Build(SourcePath, SourceText, SelectedTarget));
        ApplyBuildArtifacts(build);
        AppendOutput(FormatBuildResult("Build (for debug)", build));

        if (!build.Success || build.OutputFile == null)
        {
            StatusText = "Debug aborted (build failed)";
            return;
        }

        if (!_debugHost.StartDirectBackend())
        {
            StatusText = DebugBackendFactory.GetUnavailableReason()
                         ?? "Debug unavailable (install MinGW GDB or LLDB with Python 3.11)";
            IsDebugPaused = false;
            NotifyDebugCommandsChanged();
            return;
        }

        IsDebugPaused = false;
        NotifyDebugCommandsChanged();

        var sourceMap = _sourceMap;
        if (sourceMap == null && !string.IsNullOrWhiteSpace(build.SourceMapFile) && File.Exists(build.SourceMapFile))
            sourceMap = _backend.LoadSourceMap(build.SourceMapFile);

        var resolved = DebugBreakpointResolver.Resolve(
            BreakpointLines,
            SourcePath,
            build.NasmFile,
            sourceMap);

        if (resolved.Count == 0 && BreakpointLines.Count > 0)
        {
            resolved = DebugBreakpointResolver.Resolve(BreakpointLines, SourcePath, build.NasmFile, null);
        }

        StatusText = "Starting debugger...";
        var stopped = await _debugHost.LaunchAndWaitForInitialStopAsync(
            build.OutputFile,
            resolved,
            TimeSpan.FromSeconds(15));

        if ((stopped || IsPausedAfterLaunch(_debugHost.LastStopInfo))
            && !DebugStopClassifier.IsProgramEnded(_debugHost.LastStopInfo))
        {
            if (_debugHost.LastStopInfo != null)
                ApplyDebugStopState(_debugHost.LastStopInfo);
            IsDebugPaused = _debugHost.IsDirectBackendAlive;
            StatusText = "Debug paused at begin — use Step Over to advance one instruction";
        }
        else if (stopped && DebugStopClassifier.IsProgramEnded(_debugHost.LastStopInfo))
        {
            EndDebugSessionAfterTerminalStop("Debug ended — program finished before breakpoint (restart and use Step Over from entry)");
        }
        else if (_debugHost.IsDirectBackendAlive)
        {
            IsDebugPaused = false;
            StatusText = "Debug running (no stop event within 10s)";
        }
        else
        {
            IsDebugPaused = false;
            StatusText = "Debug ended — target finished (breakpoint missed?)";
            DapText += "(hint: rebuild, set breakpoint on begin, ensure target is windows-x64-msabi)" + Environment.NewLine;
        }

        NotifyDebugCommandsChanged();
        AppendOutput($"Debug session started for {build.OutputFile}");
    }

    [RelayCommand(CanExecute = nameof(CanDebugContinue))]
    private async Task DebugContinue()
    {
        IsDebugPaused = false;
        NotifyDebugCommandsChanged();
        _debugHost.ContinueDirect();
        await WaitForDebugStopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDebugStep))]
    private async Task DebugStepOver()
    {
        IsDebugPaused = false;
        NotifyDebugCommandsChanged();
        _debugHost.StepOverDirect();
        await WaitForDebugStopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDebugStep))]
    private async Task DebugStepInto()
    {
        IsDebugPaused = false;
        NotifyDebugCommandsChanged();
        _debugHost.StepIntoDirect();
        await WaitForDebugStopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDebugStep))]
    private async Task DebugStepOut()
    {
        IsDebugPaused = false;
        NotifyDebugCommandsChanged();
        _debugHost.StepOutDirect();
        await WaitForDebugStopAsync();
    }

    [RelayCommand(CanExecute = nameof(CanDebugStop))]
    private async Task DebugStop()
    {
        _suppressDebugStopEvents = true;
        StatusText = "Stopping debug...";
        NotifyDebugCommandsChanged();

        await Task.Run(() =>
        {
            _debugHost.KillDirect();
            _debugHost.Stop();
        });

        _suppressDebugStopEvents = false;
        IsDebugPaused = false;
        CurrentDebugLine = 0;
        StatusText = "Debug stopped";
        NotifyDebugCommandsChanged();
    }

    private static bool IsPausedAfterLaunch(DebugStopInfo? info)
    {
        if (info == null || DebugStopClassifier.IsProgramEnded(info))
            return false;

        return string.Equals(info.Frames.FirstOrDefault()?.Name, "_start", StringComparison.OrdinalIgnoreCase)
               || info.Reason is "unknown" or "signal-received" or "breakpoint-hit";
    }

    private void ApplyDebugStopState(DebugStopInfo info)
    {
        var frame = info.Frames.FirstOrDefault();
        var rip = DebugLocationMapper.ParseAddress(frame?.Address)
                  ?? _debugHost.GetCurrentInstructionPointer();
        if (rip != null)
        {
            var loc = DebugLocationMapper.MapRip(rip.Value, DisasmText, _sourceMap, _rawNasmText);
            if (loc.SourceLine is > 0)
                CurrentDebugLine = loc.SourceLine.Value;
            else if (BreakpointLines.Count > 0)
                CurrentDebugLine = BreakpointLines.Min();
            if (loc.NasmLine is > 0)
                HighlightedNasmLine = loc.NasmLine.Value;
            if (!string.IsNullOrWhiteSpace(loc.Instruction))
                StatusText = $"Paused @ 0x{rip.Value:x} — {loc.Instruction}";
        }
        else if (string.Equals(frame?.Name, "_start", StringComparison.OrdinalIgnoreCase)
                 && BreakpointLines.Count > 0)
        {
            CurrentDebugLine = BreakpointLines.Min();
            StatusText = "Paused at begin — use Step Over to execute one instruction";
        }

        if (DebugStopClassifier.IsProgramEnded(info))
        {
            IsDebugPaused = false;
            CurrentDebugLine = 0;
            var msg = info.Reason == "exited"
                ? "Debug ended — step failed (program exited). Restart Debug and use Step Over again."
                : "Debug ended — program finished (use Step Over from entry on next run)";
            EndDebugSessionAfterTerminalStop(msg);
            return;
        }

        IsDebugPaused = _debugHost.IsDirectBackendAlive;
        NotifyDebugCommandsChanged();
    }

    private bool CanStartDebug() => AssemblyLabFeatures.DebugEnabled && CanExecuteGatedActions();

    private bool CanDebugContinue() =>
        AssemblyLabFeatures.DebugEnabled && IsDebugPaused && _debugHost.IsDirectBackendAlive;

    private bool CanDebugStep() =>
        AssemblyLabFeatures.DebugEnabled && IsDebugPaused && _debugHost.IsDirectBackendAlive;

    private bool CanDebugStop() => AssemblyLabFeatures.DebugEnabled && _debugHost.IsRunning;

    private async Task WaitForDebugStopAsync()
    {
        var stopped = await _debugHost.WaitForStopAsync(TimeSpan.FromSeconds(15));
        if (stopped && !DebugStopClassifier.IsProgramEnded(_debugHost.LastStopInfo))
        {
            if (_debugHost.LastStopInfo != null)
                ApplyDebugStopState(_debugHost.LastStopInfo);
        }
        else if (stopped && DebugStopClassifier.IsProgramEnded(_debugHost.LastStopInfo))
            EndDebugSessionAfterTerminalStop("Debug ended — program finished");
        else if (!_debugHost.IsDirectBackendAlive)
        {
            IsDebugPaused = false;
            StatusText = "Debug session ended";
        }
        else
        {
            StatusText = "Debug running...";
        }

        NotifyDebugCommandsChanged();
    }

    private void EndDebugSessionAfterTerminalStop(string statusText)
    {
        if (!_debugHost.IsRunning)
        {
            IsDebugPaused = false;
            StatusText = statusText;
            return;
        }

        _debugHost.KillDirect();
        _debugHost.Stop();
        IsDebugPaused = false;
        CurrentDebugLine = 0;
        StatusText = statusText;
        NotifyDebugCommandsChanged();
    }

    private void NotifyDebugCommandsChanged()
    {
        DebugCommand.NotifyCanExecuteChanged();
        DebugContinueCommand.NotifyCanExecuteChanged();
        DebugStepOverCommand.NotifyCanExecuteChanged();
        DebugStepIntoCommand.NotifyCanExecuteChanged();
        DebugStepOutCommand.NotifyCanExecuteChanged();
        DebugStopCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsDebugSessionActive));
        OnPropertyChanged(nameof(IsDebugFeatureEnabled));
    }

    private void NotifyMcpCommandsChanged()
    {
        StartMcpCommand.NotifyCanExecuteChanged();
        ListMcpToolsCommand.NotifyCanExecuteChanged();
        McpExplainCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(IsMcpFeatureEnabled));
    }

    private bool CanUseMcp() => AssemblyLabFeatures.McpEnabled;

    private void ReleaseDebugSessionForBuild()
    {
        if (!_debugHost.IsRunning)
            return;

        _debugHost.Stop();
        IsDebugPaused = false;
        CurrentDebugLine = 0;
        NotifyDebugCommandsChanged();
    }

    [RelayCommand(CanExecute = nameof(CanExecuteGatedActions))]
    private async Task ExportProofBundle()
    {
        if (!EnsureOpenFileForBuild())
            return;

        StatusText = "Exporting proof bundle...";
        var result = await Task.Run(() => _backend.ExportProofBundle(SourcePath, SourceText, SelectedTarget));
        ApplyBuildArtifacts(result);
        AppendOutput(FormatBuildResult("Proof bundle", result));

        if (result.Success && result.ProofBundleDir != null)
        {
            var capPath = Path.Combine(result.ProofBundleDir, "capabilities.json");
            if (File.Exists(capPath))
            {
                var capText = await File.ReadAllTextAsync(capPath);
                if (capText != CapabilitiesText)
                    CapabilitiesText = capText;
            }
        }

        StatusText = result.Success ? "Proof bundle complete" : "Proof bundle failed";
    }

    [RelayCommand]
    private void OpenBuildFolder()
    {
        if (SourcePath is "(unsaved)" or "")
        {
            StatusText = "Save source before opening build folder";
            return;
        }

        var dir = AssemblyLabBackend.GetBuildOutputDir(SourcePath);
        Directory.CreateDirectory(dir);
        try
        {
            if (OperatingSystem.IsWindows())
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = false
                });
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("open", dir);
            }
            else
            {
                Process.Start("xdg-open", dir);
            }

            StatusText = $"Opened build folder: {dir}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open build folder: {ex.Message}";
        }
    }

    [RelayCommand(CanExecute = nameof(CanUseMcp))]
    private async Task StartMcp()
    {
        if (!AssemblyLabFeatures.McpEnabled)
        {
            StatusText = AssemblyLabFeatures.McpDisabledMessage;
            return;
        }

        StatusText = "Starting MCP host...";
        var initJson = await Task.Run(async () =>
        {
            _mcpHost.Start(_repoRoot);
            var init = await _mcpHost.InitializeAsync();
            await _mcpHost.SendInitializedNotificationAsync();
            return init;
        });

        McpText = FormatMcpInitResult(initJson);
        StatusText = "MCP initialized";
    }

    [RelayCommand(CanExecute = nameof(CanUseMcp))]
    private async Task ListMcpTools()
    {
        if (!AssemblyLabFeatures.McpEnabled)
        {
            StatusText = AssemblyLabFeatures.McpDisabledMessage;
            return;
        }

        StatusText = "Listing MCP tools...";
        var json = await Task.Run(() => _mcpHost.ListToolsAsync());
        McpText = FormatMcpTools(json);
        StatusText = "MCP tools listed";
    }

    [RelayCommand(CanExecute = nameof(CanUseMcp))]
    private async Task McpExplain()
    {
        if (!AssemblyLabFeatures.McpEnabled)
        {
            StatusText = AssemblyLabFeatures.McpDisabledMessage;
            return;
        }

        if (!EnsureOpenFileForBuild())
            return;

        StatusText = "MCP explain running...";
        var json = await Task.Run(() => _mcpHost.ExplainCurrentSourceAsync(SourcePath, SourceText, SelectedTarget));
        McpText = FormatMcpToolResult(json);
        StatusText = "MCP explain completed";
    }

    [RelayCommand]
    private void ExplainRepair()
    {
        if (!EnsureOpenFileForBuild())
            return;

        _lastAgentJson = _backend.ExplainForAgent(SourcePath, SourceText, SelectedTarget);
        AgentText = FormatAgentExplain(_lastAgentJson);
        StatusText = "Agent explain/repair report updated";
    }

    [RelayCommand]
    private void ApplySuggestedFix()
    {
        if (string.IsNullOrWhiteSpace(_lastAgentJson))
        {
            ExplainRepair();
            if (string.IsNullOrWhiteSpace(_lastAgentJson))
                return;
        }

        var result = _backend.ApplySuggestedFix(SourceText, _lastAgentJson);
        if (!result.Success)
        {
            StatusText = $"Apply fix failed: {result.Message}";
            return;
        }

        _suppressDirty = true;
        try
        {
            SourceText = result.PatchedSource;
        }
        finally
        {
            _suppressDirty = false;
        }

        IsDirty = true;
        StatusText = result.Message;
        _ = DebouncedCompileAsync();
    }

    [RelayCommand]
    private void GoToDiagnostic(LabDiagnosticItem? item)
    {
        if (item == null)
            return;

        NavigateToSourceLine(item.Line);
    }

    [RelayCommand]
    private void NavigateToSourceLine(int line)
    {
        if (line <= 0)
            return;

        var nasmLine = _backend.FindNasmLineForSource(_sourceMap, line);
        if (nasmLine != null && !string.IsNullOrEmpty(_rawNasmText))
        {
            HighlightedNasmLine = nasmLine.Value;
            NasmText = _backend.HighlightNasmLine(_rawNasmText, nasmLine.Value);
            SelectedTabIndex = 2;
            StatusText = $"Source L{line} -> NASM L{nasmLine}";
        }
        else
        {
            StatusText = $"Source line {line} (no NASM mapping yet)";
        }
    }

    private async Task DebouncedCompileAsync()
    {
        _debounceCts?.Cancel();
        _debounceCts = new CancellationTokenSource();
        var token = _debounceCts.Token;
        try
        {
            await Task.Delay(500, token);
            if (token.IsCancellationRequested)
                return;

            var path = SourcePath;
            var text = SourceText;
            var target = SelectedTarget;

            var result = await Task.Run(() => _backend.Compile(path, text, target), token);
            var capText = _backend.SummarizeCapabilities(_backend.AnalyzeCapabilities(text));
            var planText = _backend.GetPlanText(path, target);

            if (token.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Diagnostics.Clear();
                foreach (var d in result.Diagnostics)
                    Diagnostics.Add(d);

                var nextIr = result.IrText ?? "";
                if (nextIr != IrText)
                    IrText = nextIr;

                var nextNasm = result.NasmText ?? "";
                if (nextNasm != _rawNasmText)
                {
                    _rawNasmText = nextNasm;
                    if (_rawNasmText != NasmText)
                        NasmText = _rawNasmText;
                }

                var nextAbi = result.AbiText ?? "";
                if (nextAbi != AbiText)
                    AbiText = nextAbi;

                _sourceMap = result.SourceMap;
                RefreshDisasmText();

                if (capText != CapabilitiesText)
                    CapabilitiesText = capText;
                if (planText != PlanText)
                    PlanText = planText;

                StatusText = result.Success
                    ? $"Compile OK - {Diagnostics.Count} diagnostic(s)"
                    : $"Compile failed - {Diagnostics.Count} diagnostic(s)";
            }, DispatcherPriority.Background);
        }
        catch (OperationCanceledException)
        {
            // expected for typing debounce
        }
    }

    private async Task LoadProjectAsync(string manifestPath)
    {
        try
        {
            var fullManifest = Path.GetFullPath(manifestPath);
            var projectDir = Path.GetDirectoryName(fullManifest) ?? "";
            var manifest = ProjectManifest.Load(fullManifest);

            var sourceRelative = manifest.Sources.TryGetValue("main", out var mainRel)
                ? mainRel
                : manifest.Sources.Values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(sourceRelative))
            {
                StatusText = "Project manifest has no source entries";
                return;
            }

            var sourcePath = Path.GetFullPath(Path.Combine(projectDir, sourceRelative));
            if (!File.Exists(sourcePath))
            {
                StatusText = $"Project source not found: {sourceRelative}";
                return;
            }

            var sourceText = await File.ReadAllTextAsync(sourcePath);
            await LoadEditorStateAsync(
                sourcePath,
                sourceText,
                projectDir,
                $"Mode: project ({Path.GetFileName(projectDir)})");

            if (!string.IsNullOrWhiteSpace(manifest.Target) &&
                TargetChoices.Contains(manifest.Target, StringComparer.OrdinalIgnoreCase))
            {
                SelectedTarget = manifest.Target;
            }

            PlanApproved = true;
            StatusText = $"Opened project {Path.GetFileName(projectDir)}";
        }
        catch (Exception ex)
        {
            StatusText = $"Open project failed: {ex.Message}";
        }
    }

    private async Task LoadEditorStateAsync(string path, string text, string folder, string modeText)
    {
        _suppressDirty = true;
        try
        {
            SourcePath = path;
            ProjectFolder = folder;
            OpenModeText = modeText;
            SourceText = text;
        }
        finally
        {
            _suppressDirty = false;
        }

        _diffBaselineText = text;
        IsDirty = false;
        RefreshDiffText();
        RefreshBuildOutputDir();
        _lastBuiltOutputFile = null;
        _sourceMap = null;
        _rawNasmText = "";
        NasmText = "";
        RefreshDisasmText();
        await DebouncedCompileAsync();
    }

    private bool EnsureOpenFileForBuild()
    {
        if (SourcePath is "(unsaved)" or "")
        {
            StatusText = "Open or save a .hla64 source file first";
            return false;
        }

        return true;
    }

    private void ApplyBuildArtifacts(LabBuildResult result)
    {
        if (!string.IsNullOrEmpty(result.NasmText) && result.NasmText != _rawNasmText)
        {
            _rawNasmText = result.NasmText;
            if (_rawNasmText != NasmText)
                NasmText = _rawNasmText;
        }

        if (result.SourceMap != null)
            _sourceMap = result.SourceMap;
        else if (!string.IsNullOrEmpty(result.SourceMapFile))
            _sourceMap = _backend.LoadSourceMap(result.SourceMapFile);

        _lastBuiltOutputFile = result.OutputFile;
        RefreshDisasmText();
    }

    private void RefreshToolchainInfo()
    {
        var info = LabToolchainService.Detect();
        ToolchainText = LabToolchainService.Summarize(info);
    }

    private void RefreshDisasmText()
    {
        var binaryPath = AssemblyLabBackend.ResolveOutputBinary(SourcePath, SelectedTarget, _lastBuiltOutputFile);
        var next = _backend.GetDisasmText(_rawNasmText, _sourceMap, binaryPath, SourcePath, SelectedTarget);
        if (next != DisasmText)
            DisasmText = next;
    }

    private void RefreshDiffText()
    {
        var next = _backend.GetDiffText(_diffBaselineText, SourceText);
        if (next != DiffText)
            DiffText = next;
    }

    private void RefreshBuildOutputDir()
    {
        BuildOutputDir = AssemblyLabBackend.GetBuildOutputDir(SourcePath);
    }

    private void RefreshWindowTitle()
    {
        var name = SourcePath is "(unsaved)" or ""
            ? "Untitled"
            : Path.GetFileName(SourcePath);
        WindowTitle = $"{name}{(IsDirty ? "*" : "")} - HlaX64 Assembly Lab";
    }

    private void AppendOutput(string text)
    {
        OutputText = string.IsNullOrEmpty(OutputText)
            ? text
            : OutputText + Environment.NewLine + text;
    }

    private static string FormatAgentExplain(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            sb.AppendLine($"success: {ReadString(root, "success")}");
            sb.AppendLine($"target: {ReadString(root, "target")}");
            sb.AppendLine();
            if (root.TryGetProperty("diagnostics", out var diags))
            {
                sb.AppendLine("diagnostics:");
                foreach (var d in diags.EnumerateArray())
                {
                    var code = ReadString(d, "code");
                    var line = ReadString(d, "line");
                    var message = ReadString(d, "message");
                    sb.AppendLine($"  L{line} {code}: {message}");
                    if (d.TryGetProperty("suggestedFix", out var fix) && fix.ValueKind != JsonValueKind.Null)
                        sb.AppendLine($"    suggestedFix: {fix.GetRawText()}");
                }
            }

            if (root.TryGetProperty("abiIssues", out var abi) && abi.ValueKind == JsonValueKind.Array && abi.GetArrayLength() > 0)
            {
                sb.AppendLine();
                sb.AppendLine("abiIssues:");
                foreach (var issue in abi.EnumerateArray())
                    sb.AppendLine($"  {issue}");
            }

            if (root.TryGetProperty("clobberWarnings", out var clobber) &&
                clobber.ValueKind == JsonValueKind.Array &&
                clobber.GetArrayLength() > 0)
            {
                sb.AppendLine();
                sb.AppendLine("clobberWarnings:");
                foreach (var warning in clobber.EnumerateArray())
                    sb.AppendLine($"  {warning}");
            }

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return json;
        }
    }

    private static string FormatMcpTools(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            sb.AppendLine("MCP tools:");

            if (root.TryGetProperty("error", out var error))
            {
                sb.AppendLine(error.GetRawText());
                return sb.ToString().TrimEnd();
            }

            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("tools", out var tools) &&
                tools.ValueKind == JsonValueKind.Array)
            {
                foreach (var tool in tools.EnumerateArray())
                {
                    var name = ReadString(tool, "name");
                    var description = ReadString(tool, "description");
                    sb.AppendLine($"- {name}: {description}");
                }
            }
            else
            {
                sb.AppendLine(json);
            }

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return json;
        }
    }

    private static string FormatMcpInitResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            sb.AppendLine("MCP initialize:");

            if (root.TryGetProperty("error", out var error))
            {
                sb.AppendLine(error.GetRawText());
                return sb.ToString().TrimEnd();
            }

            if (root.TryGetProperty("result", out var result))
            {
                sb.AppendLine($"protocolVersion: {ReadString(result, "protocolVersion")}");
                if (result.TryGetProperty("serverInfo", out var info))
                {
                    sb.AppendLine($"server: {ReadString(info, "name")} {ReadString(info, "version")}");
                }
            }
            else
            {
                sb.AppendLine(json);
            }

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return json;
        }
    }

    private static string FormatMcpToolResult(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var sb = new StringBuilder();
            sb.AppendLine("MCP tool result:");

            if (root.TryGetProperty("error", out var error))
            {
                sb.AppendLine(error.GetRawText());
                return sb.ToString().TrimEnd();
            }

            if (root.TryGetProperty("result", out var result))
            {
                if (result.TryGetProperty("isError", out var isError) && isError.ValueKind == JsonValueKind.True)
                    sb.AppendLine("isError: true");

                if (result.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in content.EnumerateArray())
                    {
                        var text = ReadString(item, "text");
                        if (!string.IsNullOrWhiteSpace(text))
                            sb.AppendLine(text);
                        else
                            sb.AppendLine(item.GetRawText());
                    }
                }
                else
                {
                    sb.AppendLine(result.GetRawText());
                }
            }
            else
            {
                sb.AppendLine(json);
            }

            return sb.ToString().TrimEnd();
        }
        catch
        {
            return json;
        }
    }

    private static string FormatBuildResult(string operation, LabBuildResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{operation}: {(result.Success ? "success" : "failed")}");
        sb.AppendLine(result.Message.TrimEnd());

        if (!string.IsNullOrWhiteSpace(result.OutputFile))
            sb.AppendLine($"output: {result.OutputFile}");
        if (!string.IsNullOrWhiteSpace(result.NasmFile))
            sb.AppendLine($"nasm: {result.NasmFile}");
        if (!string.IsNullOrWhiteSpace(result.SourceMapFile))
            sb.AppendLine($"sourcemap: {result.SourceMapFile}");
        if (!string.IsNullOrWhiteSpace(result.ProofBundleDir))
            sb.AppendLine($"proofBundle: {result.ProofBundleDir}");

        return sb.ToString().TrimEnd();
    }

    private static string ReadString(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var el))
            return "(missing)";

        return el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? "",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => "null",
            _ => el.GetRawText()
        };
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8; i++)
        {
            if (File.Exists(Path.Combine(dir, "HlaX64.slnx")))
                return dir;

            var parent = Directory.GetParent(dir);
            if (parent == null)
                break;
            dir = parent.FullName;
        }

        return Directory.GetCurrentDirectory();
    }
}
