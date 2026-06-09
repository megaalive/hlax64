using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using Avalonia.Threading;
using AvaloniaTerminal;
using HlaX64.AssemblyLab.Services;

namespace HlaX64.AssemblyLab.Controls;

public partial class LabTerminalControl : UserControl, ILabTerminalHost
{
    private readonly TerminalControlModel _model = new(new TerminalOptions
    {
        ReflowOnResize = false,
        Scrollback = 5000,
        TermName = "xterm-256color"
    });

    private readonly PtyInputWriter _ptyInput = new();

    private InteractivePtySession? _session;
    private string _workingDirectory = Environment.CurrentDirectory;
    private string? _repoRoot;
    private (int cols, int rows)? _lastResize;
    private bool _shellStartRequested;
    private bool _tabVisible;
    private Task? _shellStartTask;
    private DispatcherTimer? _caretSyncTimer;
    private DispatcherTimer? _caretBlinkTimer;
    private Action? _wrappedUpdateUi;
    private Size? _measuredCellSize;
    private bool _overlayCaretVisible = true;

    public LabTerminalControl()
    {
        InitializeComponent();
        TerminalView.Model = _model;
        TerminalView.CaretBrush = Brushes.Transparent;
        _model.UserInput += OnUserInput;
        _model.SizeChanged += OnTerminalSizeChanged;

        _caretSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(48) };
        _caretSyncTimer.Tick += (_, _) =>
        {
            _caretSyncTimer.Stop();
            UpdateOverlayCaret();
        };

        _caretBlinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _caretBlinkTimer.Tick += (_, _) =>
        {
            _overlayCaretVisible = !_overlayCaretVisible;
            UpdateOverlayCaretOpacity();
        };

        TerminalView.GotFocus += (_, _) =>
        {
            _overlayCaretVisible = true;
            _caretBlinkTimer?.Start();
            UpdateOverlayCaret();
        };
        TerminalView.LostFocus += (_, _) =>
        {
            _caretBlinkTimer?.Stop();
            _overlayCaretVisible = true;
            UpdateOverlayCaretOpacity();
        };

        TerminalView.PropertyChanged += (_, args) =>
        {
            if (args.Property.Name is nameof(TerminalControl.Model) or nameof(TerminalControl.FontFamily) or nameof(TerminalControl.FontSize))
            {
                _measuredCellSize = null;
                Dispatcher.UIThread.Post(HookTerminalUpdateUi, DispatcherPriority.Loaded);
            }
        };

        TerminalView.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnTerminalPointerWheelChanged,
            RoutingStrategies.Bubble,
            handledEventsToo: true);

        TerminalView.AddHandler(
            RangeBase.ValueChangedEvent,
            OnTerminalScrollBarValueChanged,
            RoutingStrategies.Bubble | RoutingStrategies.Tunnel,
            handledEventsToo: true);

        Loaded += (_, _) =>
        {
            HookTerminalUpdateUi();
            UpdateOverlayCaret();
        };

        DetachedFromVisualTree += (_, _) =>
        {
            StopShell();
            _ptyInput.Dispose();
        };
    }

    public void Configure(string workingDirectory, string? repoRoot)
    {
        _workingDirectory = Directory.Exists(workingDirectory) ? workingDirectory : Environment.CurrentDirectory;
        _repoRoot = repoRoot;
    }

    public void SendLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        _ = SendLineAsync(line);
    }

    public void FocusTerminal()
    {
        NotifyTabVisible();
        Dispatcher.UIThread.Post(() =>
        {
            TerminalView.Focus(NavigationMethod.Pointer);
            UpdateOverlayCaret();
        });
    }

    public void NotifyTabVisible()
    {
        _tabVisible = true;
        TryStartShellWhenReady();
    }

    public void Restart()
    {
        StopShell();
        _shellStartRequested = false;
        _shellStartTask = null;
        _tabVisible = true;
        RequestShellStart();
    }

    public void StopShell()
    {
        _model.UserInput -= OnUserInput;
        _model.SizeChanged -= OnTerminalSizeChanged;

        _ptyInput.Detach();
        _session?.Dispose();
        _session = null;
        _lastResize = null;
        _shellStartRequested = false;
        _shellStartTask = null;
        _caretSyncTimer?.Stop();
        _caretBlinkTimer?.Stop();
        _model.UserInput += OnUserInput;
        _model.SizeChanged += OnTerminalSizeChanged;
        HideOverlayCaret();
        TerminalView.CaretBrush = Brushes.Transparent;
    }

    private void HookTerminalUpdateUi()
    {
        if (ReferenceEquals(_model.UpdateUI, _wrappedUpdateUi))
            return;

        var inner = _model.UpdateUI;
        _wrappedUpdateUi = () =>
        {
            inner?.Invoke();
            UpdateOverlayCaret();
        };
        _model.UpdateUI = _wrappedUpdateUi;
    }

    private void OnTerminalPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        ScheduleOverlayCaretUpdate();
        Dispatcher.UIThread.Post(UpdateOverlayCaret, DispatcherPriority.Render);
    }

    private void OnTerminalScrollBarValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        ScheduleOverlayCaretUpdate();
        Dispatcher.UIThread.Post(UpdateOverlayCaret, DispatcherPriority.Render);
    }

    private void RequestShellStart()
    {
        if (_shellStartRequested || !_tabVisible)
            return;

        if (!HasValidTerminalSize())
            return;

        _shellStartRequested = true;
        _shellStartTask = StartShellAsync();
    }

    private void TryStartShellWhenReady()
    {
        if (_shellStartRequested || !_tabVisible || !HasValidTerminalSize())
            return;

        RequestShellStart();
    }

    private bool HasValidTerminalSize()
        => _model.Terminal.Cols >= 20 && _model.Terminal.Rows >= 5;

    private async Task EnsureShellStartedAsync()
    {
        if (_session?.IsRunning == true)
            return;

        _tabVisible = true;
        for (var attempt = 0; attempt < 40 && !HasValidTerminalSize(); attempt++)
            await Task.Delay(25).ConfigureAwait(false);

        RequestShellStart();
        if (_shellStartTask != null)
            await _shellStartTask.ConfigureAwait(false);
    }

    private async Task SendLineAsync(string line)
    {
        try
        {
            await EnsureShellStartedAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _model.Send(line + "\r");
                UpdateOverlayCaret();
                ScheduleOverlayCaretUpdate();
            });
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _model.Feed($"\r\n[Terminal command failed: {ex.Message}]\r\n"));
        }
    }

    private async Task StartShellAsync()
    {
        try
        {
            _session?.Dispose();
            _session = new InteractivePtySession();
            _ptyInput.Attach(_session);
            _session.DataReceived += OnSessionDataReceived;
            _session.Exited += OnSessionExited;

            var cols = Math.Max(_model.Terminal.Cols, 80);
            var rows = Math.Max(_model.Terminal.Rows, 24);
            await _session.StartAsync(_workingDirectory, cols, rows, _repoRoot).ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                ApplyShellResize(Math.Max(_model.Terminal.Cols, 1), Math.Max(_model.Terminal.Rows, 1));
                UpdateOverlayCaret();
                ScheduleOverlayCaretUpdate();
            });
        }
        catch (Exception ex)
        {
            _shellStartRequested = false;
            _shellStartTask = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
                _model.Feed($"\r\n[Terminal failed to start: {ex.Message}]\r\n"));
        }
    }

    private void OnUserInput(byte[] data)
    {
        if (data.Length == 0)
            return;

        _overlayCaretVisible = true;
        _ptyInput.Enqueue(NormalizeTerminalInput(data));
        UpdateOverlayCaret();
    }

    private void OnSessionDataReceived(byte[] data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _model.Feed(data, data.Length);
            UpdateOverlayCaret();
            ScheduleOverlayCaretUpdate();
        }, DispatcherPriority.Background);
    }

    private void ScheduleOverlayCaretUpdate()
    {
        _caretSyncTimer?.Stop();
        _caretSyncTimer?.Start();
    }

    private void OnSessionExited(int exitCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _model.Feed($"\r\n[Shell exited with code {exitCode} — press Restart]\r\n");
            _shellStartRequested = false;
            _shellStartTask = null;
            UpdateOverlayCaret();
            ScheduleOverlayCaretUpdate();
        });
    }

    private void OnTerminalSizeChanged(int cols, int rows, double width, double height)
    {
        _ = width;
        _ = height;
        ApplyShellResize(cols, rows);
        TryStartShellWhenReady();
        ScheduleOverlayCaretUpdate();
    }

    private void ApplyShellResize(int cols, int rows)
    {
        var normalized = (Math.Max(cols, 1), Math.Max(rows, 1));
        if (_lastResize == normalized)
            return;

        _lastResize = normalized;
        _session?.Resize(normalized.Item1, normalized.Item2);
    }

    private void UpdateOverlayCaret()
    {
        TerminalView.CaretBrush = Brushes.Transparent;

        if (!TerminalTypingCaret.TryGetViewportPosition(_model, out var column, out var row))
        {
            HideOverlayCaret();
            return;
        }

        var cellWidth = GetCellWidth();
        var cellHeight = GetCellHeight();
        var rows = _model.Terminal.Rows;

        row = Math.Clamp(row, 0, Math.Max(rows - 1, 0));

        var textHeight = cellHeight * rows;
        CaretOverlay.Height = textHeight;
        CaretOverlay.ClipToBounds = true;

        OverlayCaret.Width = Math.Max(cellWidth - 1, 1);
        OverlayCaret.Height = cellHeight;
        Canvas.SetLeft(OverlayCaret, column * cellWidth);
        Canvas.SetTop(OverlayCaret, row * cellHeight);
        OverlayCaret.IsVisible = true;
        UpdateOverlayCaretOpacity();
    }

    private void UpdateOverlayCaretOpacity()
    {
        if (!OverlayCaret.IsVisible)
            return;

        var show = _overlayCaretVisible || !TerminalView.IsFocused;
        OverlayCaret.Opacity = show ? 1 : 0;
    }

    private void HideOverlayCaret()
    {
        OverlayCaret.IsVisible = false;
    }

    private Size GetCellSize()
    {
        if (_measuredCellSize is { } cached)
            return cached;

        try
        {
            var typeface = new Typeface(new FontFamily(TerminalView.FontFamily), FontStyle.Normal, FontWeight.Normal);
            var shaped = TextShaper.Current.ShapeText("a", new TextShaperOptions(typeface.GlyphTypeface, TerminalView.FontSize));
            var run = new ShapedTextRun(shaped, new GenericTextRunProperties(typeface, TerminalView.FontSize));
            _measuredCellSize = run.Size;
        }
        catch (Exception)
        {
            _measuredCellSize = new Size(
                Math.Max(TerminalView.FontSize * 0.6, 1),
                Math.Max(TerminalView.FontSize * 1.4, 1));
        }

        return _measuredCellSize.Value;
    }

    private double GetCellWidth()
        => Math.Max(GetCellSize().Width, 1);

    private double GetCellHeight()
        => Math.Max(GetCellSize().Height, 1);

    private static byte[] NormalizeTerminalInput(byte[] input)
    {
        if (input.Length == 0)
            return input;

        if (!OperatingSystem.IsWindows())
            return input;

        var normalized = new List<byte>(input.Length + 4);
        var newline = "\r\n"u8.ToArray();

        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            if (current == '\r')
            {
                normalized.AddRange(newline);
                if (i + 1 < input.Length && input[i + 1] == '\n')
                    i++;
                continue;
            }

            normalized.Add(current);
        }

        return normalized.ToArray();
    }
}
