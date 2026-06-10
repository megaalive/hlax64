namespace HlaX64.DebugAdapter;

public sealed class GdbBackend : DebugEngineSession
{
    private static readonly string[] X64RegisterNames =
    [
        "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp",
        "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15", "rip", "eflags"
    ];

    private readonly List<string> _breakpoints = [];
    private string? _executable;
    private string? _shadowExecutable;
    private nint _win32ProcessHandle;
    private int _attachedProcessId;
    private int _attachedThreadId;
    private ulong _entryPoint;
    private byte _entryOriginalOpcode;
    private bool _entryTrapPendingRestore;
    private bool _entryTrapNeedsRipReset;
    private bool _registerQueryDisabled;
    private string? _debugTarget;
    private IReadOnlyList<DebugStackFrame> _cachedFrames = [];
    private IReadOnlyList<DebugRegister> _cachedRegisters = [];

    public override string Name => "gdb";
    public override bool IsAvailable => DebuggerProbe.TryFindGdb(out _);

    public void PrepareExecutable(string executable)
    {
        _executable = executable;
        _shadowExecutable = DebugShadowExecutable.TryCreate(executable);
    }

    public override void Launch(string executable, string[]? args = null)
    {
        if (!DebuggerProbe.TryFindGdb(out var gdbPath))
            throw new InvalidOperationException(
                "gdb backend requires gdb on PATH (Linux) or MinGW/MSYS2 gdb on Windows.");

        _executable = executable;
        if (_shadowExecutable == null)
            _shadowExecutable = DebugShadowExecutable.TryCreate(executable);

        var debugTarget = _shadowExecutable ?? executable;
        _debugTarget = debugTarget;
        StartEngine(gdbPath, "--interpreter=mi2 --quiet");

        var load = SendCommand($"-file-exec-and-symbols \"{debugTarget}\"", timeoutMs: 8000);
        if (load.Contains("^error", StringComparison.Ordinal))
            EmitOutput($"← ERROR loading executable: {load}");

        if (OperatingSystem.IsWindows()
            && NativeBinaryEntryPoint.TryGetEntryPoint(debugTarget, out var entry)
            && Win32DebugLaunch.TryStartWithEntryTrap(debugTarget, entry, out var pid, out var tid, out _entryOriginalOpcode, out _win32ProcessHandle))
        {
            _entryPoint = entry;
            _attachedProcessId = pid;
            _attachedThreadId = tid;
            _entryTrapPendingRestore = true;
            var attach = SendCommand($"-target-attach {pid}", timeoutMs: 8000);
            if (attach.Contains("^error", StringComparison.Ordinal))
            {
                EmitOutput($"← ERROR attach failed: {attach}");
                Win32DebugLaunch.Terminate(_win32ProcessHandle);
                _win32ProcessHandle = 0;
                EmitOutput("← debug aborted: Windows entry trap attach failed, so breakpoint would be missed");
                Disconnect();
            }
            else
            {
                EmitOutput($"> attached to suspended process pid={pid} (int3 trap @ 0x{entry:x})");
                WriteLine("-exec-continue");
            }

            return;
        }

        if (OperatingSystem.IsWindows())
        {
            EmitOutput("← debug aborted: Windows entry trap unavailable; MinGW GDB cannot break reliably with exec-run");
            Disconnect();
            return;
        }

        LaunchWithExecRun();
    }

    public override void SetBreakpoint(string file, int line)
    {
        if (file.EndsWith(".hla64", StringComparison.OrdinalIgnoreCase))
        {
            SetBreakpointBySymbol("_start");
            return;
        }

        QueueBreakpoint($"-break-insert \"{file}\":{line}");
    }

    public override void SetBreakpointBySymbol(string symbol)
    {
        QueueBreakpoint(BuildSymbolBreakpoint(symbol));
    }

    public override void SetBreakpointAtAddress(string symbol, int byteOffset)
    {
        if (byteOffset <= 0)
        {
            QueueBreakpoint(BuildSymbolBreakpoint(symbol));
            return;
        }

        if (!string.IsNullOrEmpty(_debugTarget ?? _executable)
            && NativeBinaryEntryPoint.TryGetEntryPoint(_debugTarget ?? _executable!, out var entry))
        {
            QueueBreakpoint($"-break-insert *0x{entry + (ulong)byteOffset:x}");
            return;
        }

        QueueBreakpoint(BuildSymbolBreakpoint(symbol));
    }

    public override void Continue()
    {
        WriteLine("-exec-continue");
    }

    public override void StepOver()
    {
        if (OperatingSystem.IsWindows())
        {
            SyncRipForGdbBeforeStep();
            if (!TryWriteLine("-exec-next-instruction"))
                WriteLine("-interpreter-exec console \"nexti\"");
        }
        else
        {
            WriteLine("-exec-next");
        }
    }

    public override void StepInto()
    {
        if (OperatingSystem.IsWindows())
        {
            SyncRipForGdbBeforeStep();
            if (!TryWriteLine("-exec-step-instruction"))
                WriteLine("-interpreter-exec console \"stepi\"");
        }
        else
        {
            WriteLine("-exec-step");
        }
    }

    public override void StepOut()
    {
        WriteLine("-exec-finish");
    }

    public override void Kill()
    {
        if (OperatingSystem.IsWindows())
        {
            TerminateInferior();
            TryKillEngineProcess();
            return;
        }

        TryWriteLine("-exec-kill");
        TryWriteLine("-target-detach");
        TerminateInferior();
    }

    public override void Disconnect()
    {
        TerminateInferior();
        TryKillEngineProcess();
        Process?.WaitForExit(500);
    }

    private void TryKillEngineProcess()
    {
        try { Process?.Kill(entireProcessTree: true); } catch { /* ignore */ }
    }

    private void TerminateInferior()
    {
        Win32DebugLaunch.Terminate(_win32ProcessHandle);
        _win32ProcessHandle = 0;

        if (_attachedProcessId > 0)
        {
            Win32DebugLaunch.TryStopProcess(_attachedProcessId);
            _attachedProcessId = 0;
        }

        _attachedThreadId = 0;

        if (!string.IsNullOrEmpty(_shadowExecutable))
            DebugProcessCleanup.ReleaseOutputFile(_shadowExecutable);

        if (!string.IsNullOrEmpty(_executable))
            DebugProcessCleanup.ReleaseOutputFile(_executable);

        DebugShadowExecutable.TryDelete(_shadowExecutable);
        _shadowExecutable = null;
    }

    protected override void HandleEngineLine(string line)
    {
        TryCompleteResultToken(line);

        if (line.Contains("Cannot insert breakpoint", StringComparison.OrdinalIgnoreCase)
            || line.Contains("Cannot access memory at address", StringComparison.OrdinalIgnoreCase))
        {
            EmitOutput("← WARN GDB could not plant breakpoint at static PE address");
        }

        if (MiOutputParser.TryParseGdbStopped(line, out var info) && info != null)
        {
            MaybeRestoreEntryTrap(info);
            info = EnrichStopInfo(info);
            _cachedFrames = info.Frames;
            RaiseStopped(info);
        }
    }

    public ulong? TryGetCurrentRip()
    {
        if (OperatingSystem.IsWindows() && _attachedThreadId > 0
            && Win32DebugLaunch.TryReadRip(_attachedThreadId, out var rip))
            return rip;

        return _entryPoint != 0 ? _entryPoint : null;
    }

    private DebugStopInfo EnrichStopInfo(DebugStopInfo info)
    {
        if (!OperatingSystem.IsWindows() || _attachedThreadId <= 0)
            return info;

        if (!Win32DebugLaunch.TryReadRip(_attachedThreadId, out var rip))
            return info;

        var frames = info.Frames.ToList();
        if (frames.Count == 0)
        {
            frames.Add(new DebugStackFrame(1, "_start", 1, 1, null, $"0x{rip:x}"));
            return info with { Frames = frames };
        }

        var frame = frames[0];
        frames[0] = frame with { Address = $"0x{rip:x}" };
        return info with { Frames = frames };
    }

    protected override IReadOnlyList<DebugStackFrame> QueryStackFrames()
    {
        if (!IsEngineAlive)
            return _cachedFrames.Count > 0
                ? _cachedFrames
                : [new DebugStackFrame(1, "_start", 1, 1, null)];

        var response = SendCommand("-stack-list-frames", timeoutMs: 1500);
        if (!string.IsNullOrEmpty(response))
        {
            var parsed = MiOutputParser.ParseGdbStackListFrames(response);
            if (parsed.Count > 0)
            {
                _cachedFrames = parsed;
                return parsed;
            }
        }

        return _cachedFrames.Count > 0
            ? _cachedFrames
            : [new DebugStackFrame(1, "_start", 1, 1, null)];
    }

    protected override IReadOnlyList<DebugRegister> QueryRegisters()
    {
        if (!IsEngineAlive || _registerQueryDisabled)
            return _cachedRegisters;

        var response = SendCommand("-data-list-register-values 1", timeoutMs: 2000);
        if (response.Contains("undefined-command", StringComparison.OrdinalIgnoreCase))
        {
            _registerQueryDisabled = true;
            return _cachedRegisters;
        }

        if (!response.Contains("^error", StringComparison.Ordinal))
        {
            var parsed = MiOutputParser.ParseGdbRegisterValues(response);
            if (parsed.Count > 0)
            {
                _cachedRegisters = parsed;
                return parsed;
            }
        }

        if (response.Contains("No registers", StringComparison.OrdinalIgnoreCase))
            _registerQueryDisabled = true;

        return _cachedRegisters;
    }

    private void LaunchWithExecRun()
    {
        SendCommand("-interpreter-exec console \"set breakpoint pending on\"", timeoutMs: 2000);

        var pending = _breakpoints.Count > 0 ? _breakpoints : [BuildEntryBreakpointCommand()];
        foreach (var bp in pending.Distinct(StringComparer.Ordinal))
        {
            if (!TryInsertBreakpoint(bp))
                EmitOutput($"← ERROR breakpoint failed: {bp}");
        }

        WriteLine("-exec-run");
    }

    private void MaybeRestoreEntryTrap(DebugStopInfo info)
    {
        if (!_entryTrapPendingRestore || _win32ProcessHandle == 0 || _entryPoint == 0)
            return;

        var addrText = info.Frames.FirstOrDefault()?.Address;
        if (TryParseAddr(addrText, out var rip) && rip != _entryPoint && rip != _entryPoint + 1)
            return;

        if (Win32DebugLaunch.TryRestoreOpcode(_win32ProcessHandle, _entryPoint, _entryOriginalOpcode))
        {
            _entryTrapPendingRestore = false;
            _entryTrapNeedsRipReset = true;
            if (_attachedThreadId > 0)
                Win32DebugLaunch.TryWriteRip(_attachedThreadId, _entryPoint);
            EmitOutput($"← restored entry opcode @ 0x{_entryPoint:x} (use Step Over from here)");
        }
    }

    /// <summary>Win32 RIP + GDB register cache must agree before MI step commands (never call from GDB reader thread).</summary>
    private void SyncRipForGdbBeforeStep()
    {
        if (!OperatingSystem.IsWindows() || _entryPoint == 0 || !IsEngineAlive)
            return;

        if (_attachedThreadId > 0)
            Win32DebugLaunch.TryWriteRip(_attachedThreadId, _entryPoint);

        if (_entryTrapNeedsRipReset)
        {
            var result = SendCommand($"-interpreter-exec console \"set $rip=0x{_entryPoint:x}\"", timeoutMs: 2000);
            if (!result.Contains("^error", StringComparison.Ordinal))
                _entryTrapNeedsRipReset = false;
            return;
        }

        if (_attachedThreadId > 0
            && Win32DebugLaunch.TryReadRip(_attachedThreadId, out var rip)
            && (rip == _entryPoint || rip == _entryPoint + 1))
        {
            Win32DebugLaunch.TryWriteRip(_attachedThreadId, _entryPoint);
            SendCommand($"-interpreter-exec console \"set $rip=0x{_entryPoint:x}\"", timeoutMs: 2000);
        }
    }

    private static bool TryParseAddr(string? text, out ulong addr)
    {
        addr = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var trimmed = text.Trim();
        if (trimmed.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[2..];

        return ulong.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out addr);
    }

    private void QueueBreakpoint(string cmd)
    {
        _breakpoints.Add(cmd);
        if (Stdin != null)
            TryInsertBreakpoint(cmd);
    }

    private bool TryInsertBreakpoint(string cmd)
    {
        foreach (var candidate in ExpandBreakpointCandidates(cmd))
        {
            var result = SendCommand(candidate, timeoutMs: 3000);
            if (!result.Contains("^error", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private IEnumerable<string> ExpandBreakpointCandidates(string cmd)
    {
        yield return cmd;

        if (!cmd.Equals("-break-insert _start", StringComparison.Ordinal))
            yield break;

        if (OperatingSystem.IsWindows())
        {
            var image = _debugTarget ?? _executable;
            if (!string.IsNullOrEmpty(image)
                && NativeBinaryEntryPoint.TryGetEntryPoint(image, out var entry))
            {
                yield return $"-break-insert *0x{entry:x}";
            }

            yield break;
        }

        if (!string.IsNullOrEmpty(_debugTarget ?? _executable)
            && NativeBinaryEntryPoint.TryGetEntryPoint(_debugTarget ?? _executable!, out var linuxEntry))
        {
            yield return $"-break-insert *0x{linuxEntry:x}";
        }
    }

    private string BuildEntryBreakpointCommand()
        => BuildSymbolBreakpoint("_start");

    private static string BuildSymbolBreakpoint(string symbol)
        => $"-break-insert {symbol}";
}
