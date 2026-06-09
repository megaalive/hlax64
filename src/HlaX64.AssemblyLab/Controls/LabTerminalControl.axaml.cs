using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaTerminal;
using HlaX64.AssemblyLab.Services;

namespace HlaX64.AssemblyLab.Controls;

public partial class LabTerminalControl : UserControl, ILabTerminalHost
{
    private static readonly TimeSpan TypingQuietPeriod = TimeSpan.FromMilliseconds(350);

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
    private bool _isAttachedToVisualTree;
    private CancellationTokenSource? _shellStartCts;
    private bool _ptyInputDisposed;
    private DispatcherTimer? _cursorSyncTimer;
    private DateTime _lastUserInputUtc = DateTime.MinValue;

    public LabTerminalControl()
    {
        InitializeComponent();
        TerminalView.Model = _model;
        _model.UserInput += OnUserInput;
        _model.SizeChanged += OnTerminalSizeChanged;

        _cursorSyncTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _cursorSyncTimer.Tick += (_, _) =>
        {
            _cursorSyncTimer.Stop();
            if (!_tabVisible || IsUserTyping())
                return;

            TerminalCursorHelper.TrySyncIfStale(_model);
        };

        TerminalView.PointerPressed += (_, _) => RestoreKeyboardFocus();

        AttachedToVisualTree += (_, _) => _isAttachedToVisualTree = true;
        DetachedFromVisualTree += (_, _) => _isAttachedToVisualTree = false;
        Unloaded += (_, _) => DisposeTerminalResources();
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

    public void FocusTerminal() => NotifyTabVisible();

    public void NotifyTabVisible()
    {
        _tabVisible = true;
        if (_session?.IsRunning == true)
        {
            _ptyInput.Attach(_session);
            RestoreTerminalLayout();
            return;
        }

        TryStartShellWhenReady();
    }

    public void NotifyTabHidden() => _tabVisible = false;

    public void Restart()
    {
        StopShell();
        _tabVisible = true;
        RequestShellStart();
    }

    public void StopShell()
    {
        CancelPendingShellStart();

        _model.UserInput -= OnUserInput;
        _model.SizeChanged -= OnTerminalSizeChanged;

        _ptyInput.Detach();
        if (_session != null)
        {
            _session.DataReceived -= OnSessionDataReceived;
            _session.Exited -= OnSessionExited;
            _session.Dispose();
            _session = null;
        }

        _lastResize = null;
        _model.UserInput += OnUserInput;
        _model.SizeChanged += OnTerminalSizeChanged;
    }

    private void RestoreTerminalLayout()
    {
        _lastResize = null;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_tabVisible)
                return;

            if (HasValidTerminalSize())
                ApplyShellResize(_model.Terminal.Cols, _model.Terminal.Rows);

            RestoreKeyboardFocus();
            if (!IsUserTyping())
                TerminalCursorHelper.TrySyncIfStale(_model);
        }, DispatcherPriority.Loaded);
    }

    private void RestoreKeyboardFocus()
        => TerminalView.Focus(NavigationMethod.Tab);

    private bool IsUserTyping()
        => DateTime.UtcNow - _lastUserInputUtc < TypingQuietPeriod;

    private void DisposeTerminalResources()
    {
        _isAttachedToVisualTree = false;
        _tabVisible = false;
        StopShell();
        if (_ptyInputDisposed)
            return;

        _ptyInputDisposed = true;
        _ptyInput.Dispose();
    }

    private void CancelPendingShellStart()
    {
        _shellStartRequested = false;
        _shellStartTask = null;

        if (_shellStartCts == null)
            return;

        try
        {
            _shellStartCts.Cancel();
        }
        catch (ObjectDisposedException) { }

        _shellStartCts.Dispose();
        _shellStartCts = null;
    }

    private void RequestShellStart()
    {
        if (_shellStartRequested || !_tabVisible)
            return;

        if (_session?.IsRunning == true)
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
            await Dispatcher.UIThread.InvokeAsync(() => _model.Send(line + "\r"));
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                _model.Feed($"\r\n[Terminal command failed: {ex.Message}]\r\n"));
        }
    }

    private async Task StartShellAsync()
    {
        CancelPendingShellStart();
        _shellStartCts = new CancellationTokenSource();
        var token = _shellStartCts.Token;

        try
        {
            _session?.Dispose();
            _session = new InteractivePtySession();
            _ptyInput.Attach(_session);
            _session.DataReceived += OnSessionDataReceived;
            _session.Exited += OnSessionExited;

            var cols = Math.Max(_model.Terminal.Cols, 80);
            var rows = Math.Max(_model.Terminal.Rows, 24);
            await _session.StartAsync(_workingDirectory, cols, rows, _repoRoot, token).ConfigureAwait(false);

            if (token.IsCancellationRequested)
            {
                _session.DataReceived -= OnSessionDataReceived;
                _session.Exited -= OnSessionExited;
                _session.Dispose();
                _session = null;
                _ptyInput.Detach();
                return;
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (HasValidTerminalSize())
                    ApplyShellResize(_model.Terminal.Cols, _model.Terminal.Rows);

                TerminalCursorHelper.TrySyncIfStale(_model);
            });
        }
        catch (OperationCanceledException)
        {
            if (_session != null)
            {
                _session.DataReceived -= OnSessionDataReceived;
                _session.Exited -= OnSessionExited;
                _session.Dispose();
                _session = null;
            }

            _ptyInput.Detach();
        }
        catch (Exception ex)
        {
            if (token.IsCancellationRequested)
                return;

            _shellStartRequested = false;
            _shellStartTask = null;
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!_isAttachedToVisualTree)
                    return;

                _model.Feed($"\r\n[Terminal failed to start: {ex.Message}]\r\n");
            });
        }
        finally
        {
            if (!token.IsCancellationRequested)
            {
                _shellStartRequested = false;
                _shellStartTask = null;
            }
        }
    }

    private void OnUserInput(byte[] data)
    {
        if (data.Length == 0 || _session?.IsRunning != true)
            return;

        _lastUserInputUtc = DateTime.UtcNow;
        _ptyInput.Attach(_session);
        _ptyInput.Enqueue(NormalizeTerminalInput(data));
    }

    private void OnSessionDataReceived(byte[] data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _model.Feed(data, data.Length);
            ScheduleCursorSyncAfterOutput();
        }, DispatcherPriority.Background);
    }

    private void ScheduleCursorSyncAfterOutput()
    {
        if (!_tabVisible || !_model.Terminal.Buffer.IsAtBottom || IsUserTyping())
            return;

        _cursorSyncTimer?.Stop();
        _cursorSyncTimer?.Start();
    }

    private void OnSessionExited(int exitCode)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_isAttachedToVisualTree)
                return;

            _model.Feed($"\r\n[Shell exited with code {exitCode} — press Restart]\r\n");
            _shellStartRequested = false;
            _shellStartTask = null;
        });
    }

    private void OnTerminalSizeChanged(int cols, int rows, double width, double height)
    {
        _ = width;
        _ = height;
        if (_tabVisible && _isAttachedToVisualTree)
        {
            ApplyShellResize(cols, rows);
            TryStartShellWhenReady();
        }
    }

    private void ApplyShellResize(int cols, int rows)
    {
        if (!_tabVisible || cols < 20 || rows < 5)
            return;

        var normalized = (cols, rows);
        if (_lastResize == normalized)
            return;

        _lastResize = normalized;
        _session?.Resize(normalized.Item1, normalized.Item2);
    }

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
