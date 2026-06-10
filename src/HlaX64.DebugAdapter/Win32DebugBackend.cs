using System.Runtime.InteropServices;
using HlaX64.Compiler.Debug;

namespace HlaX64.DebugAdapter;

/// <summary>Native Win32 debug API backend for PE targets (no GDB/LLDB required).</summary>
public sealed class Win32DebugBackend : IDebugBackend
{
    public static bool IsSupported => OperatingSystem.IsWindows();

    private readonly object _sync = new();
    private readonly HashSet<ulong> _pendingAddresses = [];
    private readonly Dictionary<ulong, Win32DebugNative.SoftwareBreakpoint> _breakpoints = [];
    private readonly AutoResetEvent _commandEvent = new(false);

    private Thread? _debugThread;
    private string? _executable;
    private string? _shadowExecutable;
    private string? _debugTarget;
    private ulong _entryPoint;
    private ulong _preferredImageBase;
    private uint _entryRva;
    private ulong _loadedImageBase;
    private bool _imageLoaded;
    private bool _loaderBreakpointHandled;
    private nint _processHandle;
    private nint _threadHandle;
    private uint _processId;
    private uint _threadId;
    private uint _activeThreadId;
    private volatile bool _engineAlive;
    private volatile bool _exitRequested;
    private volatile Win32DebugNative.DebugCommand _pendingCommand;
    private Win32DebugNative.Context64 _lastContext;
    private IReadOnlyList<DebugRegister> _cachedRegisters = [];
    private bool _breakAtEntry;
    private string? _lastNasmPath;
    private int _lastNasmLine;
    private string[] _launchArgs = [];
    private ulong? _stepOverReturnAddress;
    private EphemeralStopKind _ephemeralStopKind;
    private string? _nasmPath;

    private enum EphemeralStopKind
    {
        None,
        StepOverCall,
        StepOut
    }
    private PeDebugAddressMap.CachedMaps? _addressMaps;
    private bool _runToProcessExit;

    public string Name => "win32";
    public bool IsAvailable => OperatingSystem.IsWindows();
    public bool IsEngineAlive => _engineAlive;

    public event Action<string>? OutputReceived;
    public event Action<DebugStopInfo>? Stopped;

    public void PrepareExecutable(string executable)
    {
        _executable = executable;
        _shadowExecutable = DebugShadowExecutable.TryCreate(executable);
        _debugTarget = _shadowExecutable ?? executable;
        if (!string.IsNullOrEmpty(_debugTarget)
            && NativeBinaryEntryPoint.TryGetEntryPoint(_debugTarget, out var entry))
        {
            _entryPoint = entry;
        }

        if (!string.IsNullOrEmpty(_debugTarget))
        {
            NativeBinaryEntryPoint.TryGetPeEntryRva(_debugTarget, out _entryRva);
            if (!NativeBinaryEntryPoint.TryGetPePreferredImageBase(_debugTarget, out _preferredImageBase)
                && _entryPoint != 0
                && _entryRva != 0)
            {
                _preferredImageBase = _entryPoint - _entryRva;
            }
        }
    }

    public void PrepareDebugMaps(string executable, string nasmPath, SourceMapDocument? sourceMap)
    {
        _nasmPath = nasmPath;
        _addressMaps = PeDebugAddressMap.GetOrBuild(executable, nasmPath, sourceMap);
    }

    public void Launch(string executable, string[]? args = null)
    {
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Win32 debug backend requires Windows.");

        Stop();
        _executable = executable;
        _launchArgs = args?.Where(a => !string.IsNullOrWhiteSpace(a)).ToArray() ?? [];
        if (_shadowExecutable == null)
            PrepareExecutable(executable);
        else
            _debugTarget = _shadowExecutable ?? executable;

        _breakpoints.Clear();
        _loaderBreakpointHandled = false;
        _runToProcessExit = false;
        _imageLoaded = false;
        _loadedImageBase = 0;
        _exitRequested = false;
        _engineAlive = true;
        _debugThread = new Thread(DebugLoop)
        {
            IsBackground = true,
            Name = "HlaX64.Win32DebugBackend"
        };
        _debugThread.Start();
        var argsText = Win32ProcessCommandLine.FormatArgsForLog(_launchArgs);
        EmitOutput($"> launching {_debugTarget ?? executable} (Win32 debug API)  args: {argsText}");
    }

    public void SetBreakpoint(string file, int line)
    {
        if (file.EndsWith(".hla64", StringComparison.OrdinalIgnoreCase))
        {
            SetBreakpointBySymbol("_start");
            return;
        }

        if (file.EndsWith(".nasm", StringComparison.OrdinalIgnoreCase))
        {
            _lastNasmPath = file;
            _lastNasmLine = line;
            var target = _debugTarget ?? _executable;
            if (!string.IsNullOrEmpty(target)
                && NasmLineAddressResolver.TryResolve(target, file, line) is ulong addr)
            {
                QueueAddress(addr);
                return;
            }
        }

        SetBreakpointBySymbol("_start");
    }

    public void SetBreakpointBySymbol(string symbol)
    {
        if (symbol.Equals("_start", StringComparison.OrdinalIgnoreCase))
            _breakAtEntry = true;
    }

    public void SetBreakpointAtAddress(string symbol, int byteOffset)
    {
        if (_entryPoint == 0)
            return;

        QueueAddress(byteOffset <= 0 ? _entryPoint : _entryPoint + (ulong)byteOffset);
    }

    public void Continue()
    {
        SignalCommand(Win32DebugNative.DebugCommand.Continue);
    }

    public void StepOver() => SignalCommand(Win32DebugNative.DebugCommand.StepOver);

    public void StepInto() => SignalCommand(Win32DebugNative.DebugCommand.StepInto);

    public void StepOut() => SignalCommand(Win32DebugNative.DebugCommand.StepOut);

    public void Kill() => SignalCommand(Win32DebugNative.DebugCommand.Kill);

    public IReadOnlyList<object> GetStackFrames()
    {
        var rip = _lastContext.Rip;
        return
        [
            new
            {
                id = 1,
                name = "_start",
                line = 1,
                column = 1,
                source = (object?)null,
                address = $"0x{rip:x}"
            }
        ];
    }

    public IReadOnlyList<DebugRegister> GetRegisters()
        => _cachedRegisters.Count > 0 ? _cachedRegisters : [];

    public ulong? TryGetCurrentRip()
        => _lastContext.Rip != 0 ? _lastContext.Rip : _entryPoint != 0 ? _entryPoint : null;

    public int? TryResolveUserCallSiteSourceLine(
        string executablePath,
        string nasmPath,
        SourceMapDocument? sourceMap)
    {
        if (_processHandle == 0 || _lastContext.Rsp == 0)
            return null;

        var maps = PeDebugAddressMap.GetOrBuild(executablePath, nasmPath, sourceMap);
        for (var slot = 0; slot < 48; slot++)
        {
            var slotAddress = _lastContext.Rsp + (ulong)(slot * 8);
            var stackBytes = new byte[8];
            if (!Win32DebugNative.TryReadMemory(_processHandle, slotAddress, stackBytes))
                break;

            var candidate = BitConverter.ToUInt64(stackBytes);
            if (candidate == 0)
                continue;

            if (!PeDebugAddressMap.IsAddressInMainModule(candidate, executablePath, nasmPath))
                continue;

            if (PeDebugAddressMap.LookupCallSiteSourceLine(candidate, maps.SourceByAddress) is int line and > 0)
                return line;
        }

        return null;
    }

    public void Disconnect() => Stop();

    public void Dispose() => Stop();

    private void QueueAddress(ulong address)
    {
        lock (_sync)
        {
            _pendingAddresses.Add(address);
        }
    }

    private void SignalCommand(Win32DebugNative.DebugCommand command)
    {
        _pendingCommand = command;
        _commandEvent.Set();
    }

    private void DebugLoop()
    {
        var target = _debugTarget ?? _executable;
        if (string.IsNullOrWhiteSpace(target) || !File.Exists(target))
        {
            EmitOutput("← ERROR executable not found");
            _engineAlive = false;
            return;
        }

        var si = new Win32DebugNative.StartupInfo { cb = Marshal.SizeOf<Win32DebugNative.StartupInfo>() };
        var commandLine = Win32ProcessCommandLine.BuildCreateProcessCommandLine(target, _launchArgs);
        if (!Win32DebugNative.CreateProcess(
                null,
                commandLine,
                nint.Zero,
                nint.Zero,
                false,
                Win32DebugNative.DebugOnlyThisProcess | Win32DebugNative.CreateNoWindow,
                nint.Zero,
                null,
                ref si,
                out var pi))
        {
            EmitOutput($"← ERROR CreateProcess failed (win32={Marshal.GetLastWin32Error()})");
            _engineAlive = false;
            return;
        }

        _processHandle = pi.hProcess;
        _threadHandle = pi.hThread;
        _processId = (uint)pi.dwProcessId;
        _threadId = (uint)pi.dwThreadId;

        try
        {
            var debugEvent = new Win32DebugNative.DebugEvent();
            while (!_exitRequested && Win32DebugNative.WaitForDebugEvent(out debugEvent, 5000))
            {
                var continueStatus = Win32DebugNative.DbgContinue;
                var handled = false;

                switch (debugEvent.dwDebugEventCode)
                {
                    case Win32DebugNative.DebugEventCode.CreateProcess:
                        if (_imageLoaded || debugEvent.dwProcessId != _processId)
                            break;

                        _imageLoaded = true;
                        _processHandle = debugEvent.u.CreateProcessInfo.hProcess;
                        _threadHandle = debugEvent.u.CreateProcessInfo.hThread;
                        _processId = debugEvent.dwProcessId;
                        _threadId = debugEvent.dwThreadId;
                        _activeThreadId = debugEvent.dwThreadId;
                        OnImageLoaded((ulong)debugEvent.u.CreateProcessInfo.lpBaseOfImage);
                        EmitOutput($"> debug attached pid={_processId} entry=0x{_entryPoint:x}");
                        break;

                    case Win32DebugNative.DebugEventCode.Exception:
                        handled = HandleExceptionDebugEvent(debugEvent, ref continueStatus);
                        break;

                    case Win32DebugNative.DebugEventCode.ExitProcess:
                        _runToProcessExit = false;
                        EmitOutput($"← process exited code={debugEvent.u.ExitProcess.dwExitCode}");
                        RaiseStopped(BuildStopInfo("exited", debugEvent.u.ExitProcess.dwExitCode));
                        _exitRequested = true;
                        break;
                }

                if (!handled && debugEvent.dwDebugEventCode != Win32DebugNative.DebugEventCode.ExitProcess)
                    Win32DebugNative.ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId, continueStatus);
            }
        }
        catch (Exception ex)
        {
            EmitOutput($"← ERROR debug loop: {ex.Message}");
        }
        finally
        {
            TerminateInferior();
            _engineAlive = false;
        }
    }

    private bool HandleExceptionDebugEvent(
        Win32DebugNative.DebugEvent debugEvent,
        ref uint continueStatus)
    {
        var code = debugEvent.u.Exception.ExceptionRecord.ExceptionCode;
        _activeThreadId = debugEvent.dwThreadId;

        if (_runToProcessExit
            && code is Win32DebugNative.ExceptionBreakpoint or Win32DebugNative.ExceptionSingleStep)
        {
            Win32DebugNative.ContinueDebugEvent(
                debugEvent.dwProcessId,
                debugEvent.dwThreadId,
                Win32DebugNative.DbgContinue);
            return true;
        }

        if (code == Win32DebugNative.ExceptionBreakpoint)
        {
            if (!TryReadContext(debugEvent.dwThreadId, out var context, debugEventSuspended: true))
            {
                EmitOutput($"← WARN GetThreadContext failed err={Marshal.GetLastWin32Error()}");
                return false;
            }

            var rip = context.Rip;
            var exceptionAddress = (ulong)debugEvent.u.Exception.ExceptionRecord.ExceptionAddress;
            if (TryHandleInitialLoaderBreakpoint(exceptionAddress, rip, ref context))
                return true;

            var bpAddress = ResolveBreakpointAddress(rip, exceptionAddress);
            if (TryHitSoftwareBreakpoint(bpAddress, ref context, out var stopReason))
            {
                _lastContext = context;
                _cachedRegisters = Win32DebugNative.BuildRegisterList(context);
                RaiseStopped(BuildStopInfo(stopReason));
                if (!WaitForCommand())
                    return true;

                ExecutePendingCommand(ref context);
                return true;
            }

            EmitOutput($"← continuing system breakpoint @ 0x{bpAddress:x}");
            Win32DebugNative.ContinueDebugEvent(debugEvent.dwProcessId, debugEvent.dwThreadId, Win32DebugNative.DbgContinue);
            return true;
        }

        if (code == Win32DebugNative.ExceptionSingleStep)
        {
            if (!TryReadContext(debugEvent.dwThreadId, out var context, debugEventSuspended: true))
                return false;

            _lastContext = context;
            _cachedRegisters = Win32DebugNative.BuildRegisterList(context);
            RaiseStopped(BuildStopInfo("step"));
            if (!WaitForCommand())
                return true;

            ExecutePendingCommand(ref context);
            return true;
        }

        if (code == 0xC0000005)
        {
            EmitOutput("← access violation — terminating debuggee");
            _exitRequested = true;
            TerminateInferior();
            RaiseStopped(BuildStopInfo("exited-signalled"));
            return true;
        }

        return false;
    }

    private void ExecutePendingCommand(ref Win32DebugNative.Context64 context)
    {
        switch (_pendingCommand)
        {
            case Win32DebugNative.DebugCommand.Kill:
                _exitRequested = true;
                TerminateInferior();
                return;

            case Win32DebugNative.DebugCommand.Continue:
                Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                return;

            case Win32DebugNative.DebugCommand.StepOver:
                if (TryBeginStepOverExitJump(ref context))
                {
                    Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                    return;
                }

                if (TryBeginStepOverCall(ref context))
                {
                    Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                    return;
                }

                if (TrySingleStep(_activeThreadId, ref context))
                {
                    Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                }
                else
                {
                    EmitOutput($"← WARN SetThreadContext failed err={Marshal.GetLastWin32Error()}");
                }
                return;

            case Win32DebugNative.DebugCommand.StepInto:
                if (TrySingleStep(_activeThreadId, ref context))
                {
                    Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                }
                else
                {
                    EmitOutput($"← WARN SetThreadContext failed err={Marshal.GetLastWin32Error()}");
                }
                return;

            case Win32DebugNative.DebugCommand.StepOut:
                if (TryBeginStepOut(ref context))
                {
                    Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                }
                else
                {
                    EmitOutput("← WARN step-out failed, single-step fallback");
                    if (TrySingleStep(_activeThreadId, ref context))
                    {
                        Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
                    }
                }

                return;
        }
    }

    private bool WaitForCommand()
    {
        _pendingCommand = Win32DebugNative.DebugCommand.None;
        while (_pendingCommand == Win32DebugNative.DebugCommand.None && !_exitRequested)
        {
            if (!_commandEvent.WaitOne(100))
                continue;
        }

        return !_exitRequested;
    }

    private bool TryBeginStepOverExitJump(ref Win32DebugNative.Context64 context)
    {
        if (_processHandle == 0
            || _addressMaps == null
            || string.IsNullOrWhiteSpace(_debugTarget)
            || string.IsNullOrWhiteSpace(_nasmPath))
        {
            return false;
        }

        var bytes = new byte[5];
        if (!Win32DebugNative.TryReadMemory(_processHandle, context.Rip, bytes) || bytes[0] != 0xE9)
            return false;

        var rel = BitConverter.ToInt32(bytes, 1);
        var target = context.Rip + 5 + (ulong)(long)rel;
        if (!PeDebugAddressMap.IsExitJumpTarget(target, _debugTarget, _nasmPath, _addressMaps))
            return false;

        ClearEphemeralBreakpoints();
        _runToProcessExit = true;
        EmitOutput("← finishing program (ExitProcess) — continuing to process exit");
        return true;
    }

    private bool TryBeginStepOverCall(ref Win32DebugNative.Context64 context)
    {
        if (_processHandle == 0)
            return false;

        if (!X64CallInstruction.TryGetCallReturnRip(_processHandle, context.Rip, out var returnRip))
            return false;

        ClearEphemeralBreakpoints();
        _stepOverReturnAddress = returnRip;
        _ephemeralStopKind = EphemeralStopKind.StepOverCall;
        var bp = GetOrCreateBreakpoint(returnRip);
        bp.IsEphemeral = true;
        if (!PlantBreakpoint(bp))
        {
            _stepOverReturnAddress = null;
            _ephemeralStopKind = EphemeralStopKind.None;
            return false;
        }

        EmitOutput($"← step-over call until 0x{returnRip:x}");
        return true;
    }

    private bool TryBeginStepOut(ref Win32DebugNative.Context64 context)
    {
        if (_processHandle == 0 || context.Rsp == 0)
            return false;

        if (_addressMaps == null
            || string.IsNullOrWhiteSpace(_debugTarget)
            || string.IsNullOrWhiteSpace(_nasmPath))
        {
            EmitOutput("← WARN step-out failed: debug maps not loaded");
            return false;
        }

        if (!PeDebugAddressMap.TryFindStackReturnAddress(
                context.Rsp,
                TryReadStackQword,
                _debugTarget,
                _nasmPath,
                _addressMaps,
                out var returnRip))
        {
            EmitOutput("← WARN step-out failed: no caller return address on stack");
            return false;
        }

        ClearEphemeralBreakpoints();
        _stepOverReturnAddress = returnRip;
        _ephemeralStopKind = EphemeralStopKind.StepOut;
        var bp = GetOrCreateBreakpoint(returnRip);
        bp.IsEphemeral = true;
        if (!PlantBreakpoint(bp))
        {
            _stepOverReturnAddress = null;
            _ephemeralStopKind = EphemeralStopKind.None;
            EmitOutput($"← WARN step-out failed: could not plant breakpoint @ 0x{returnRip:x}");
            return false;
        }

        EmitOutput($"← step-out until 0x{returnRip:x}");
        return true;
    }

    private bool TryReadStackQword(ulong address, out ulong value)
    {
        value = 0;
        var stackBytes = new byte[8];
        if (!Win32DebugNative.TryReadMemory(_processHandle, address, stackBytes))
            return false;

        value = BitConverter.ToUInt64(stackBytes);
        return true;
    }

    private void ClearEphemeralBreakpoints()
    {
        foreach (var address in _breakpoints.Where(p => p.Value.IsEphemeral).Select(p => p.Key).ToList())
        {
            UnplantBreakpointAt(address);
            _breakpoints.Remove(address);
        }

        _stepOverReturnAddress = null;
        _ephemeralStopKind = EphemeralStopKind.None;
    }

    private bool TrySingleStep(uint threadId, ref Win32DebugNative.Context64 context)
    {
        context.EFlags &= ~Win32DebugNative.EflagTrap;
        context.EFlags |= Win32DebugNative.EflagTrap;
        _lastContext = context;
        return TryWriteContext(threadId, ref context, debugEventSuspended: true);
    }

    private ulong ResolveBreakpointAddress(ulong rip, ulong exceptionAddress)
    {
        foreach (var candidate in new[] { exceptionAddress, rip > 0 ? rip - 1 : rip, rip })
        {
            if (candidate != 0 && _breakpoints.ContainsKey(candidate))
                return candidate;
        }

        return rip > 0 ? rip - 1 : rip;
    }

    private bool TryHitSoftwareBreakpoint(
        ulong address,
        ref Win32DebugNative.Context64 context,
        out string stopReason)
    {
        stopReason = "breakpoint-hit";
        if (!_breakpoints.TryGetValue(address, out var bp) || !bp.IsActive)
            return false;

        if (bp.IsPlanted)
        {
            if (!Win32DebugNative.TryWriteMemory(_processHandle, address, [bp.OriginalOpcode]))
                return false;
            bp.IsPlanted = false;
        }

        context.Rip = address;
        if (!TryWriteContext(_activeThreadId, ref context, debugEventSuspended: true))
            return false;

        if (bp.IsEphemeral || _stepOverReturnAddress == address)
        {
            stopReason = "step";
            var kind = _ephemeralStopKind;
            _stepOverReturnAddress = null;
            _ephemeralStopKind = EphemeralStopKind.None;
            _breakpoints.Remove(address);
            EmitOutput(kind == EphemeralStopKind.StepOut
                ? $"← step-out returned @ 0x{address:x}"
                : $"← step-over returned @ 0x{address:x}");
            return true;
        }

        EmitOutput($"← breakpoint hit @ 0x{address:x}");
        return true;
    }

    private void OnImageLoaded(ulong loadedImageBase)
    {
        _loadedImageBase = loadedImageBase;
        ResolveLoadedEntryPoint(loadedImageBase);
        RebaseQueuedAddressesToLoadedImage();
        EnsureEntryBreakpoint();
        PlantPendingBreakpoints();
    }

    private void RebaseQueuedAddressesToLoadedImage()
    {
        if (_loadedImageBase == 0 || _preferredImageBase == 0 || _loadedImageBase == _preferredImageBase)
            return;

        var slide = _loadedImageBase - _preferredImageBase;
        lock (_sync)
        {
            var rebased = _pendingAddresses
                .Select(addr => addr >= _preferredImageBase ? addr + slide : addr)
                .ToList();
            _pendingAddresses.Clear();
            foreach (var addr in rebased)
                _pendingAddresses.Add(addr);
        }
    }

    private bool IsPreferredEntryBreakpoint(ulong exceptionAddress, ulong rip)
    {
        if (_preferredImageBase == 0 || _entryRva == 0)
            return false;

        var preferredEntry = _preferredImageBase + _entryRva;
        return exceptionAddress == preferredEntry
               || rip == preferredEntry
               || (rip > 0 && rip - 1 == preferredEntry);
    }

    private bool TryHandleInitialLoaderBreakpoint(
        ulong exceptionAddress,
        ulong rip,
        ref Win32DebugNative.Context64 context)
    {
        if (_loaderBreakpointHandled || _entryPoint == 0 || !IsPreferredEntryBreakpoint(exceptionAddress, rip))
            return false;

        _loaderBreakpointHandled = true;
        UnplantBreakpointAt(_entryPoint);
        context.Rip = _entryPoint;
        if (!TryWriteContext(_activeThreadId, ref context, debugEventSuspended: true))
        {
            EmitOutput($"← WARN failed to redirect loader entry to 0x{_entryPoint:x} err={Marshal.GetLastWin32Error()}");
            return false;
        }

        _lastContext = context;
        _cachedRegisters = Win32DebugNative.BuildRegisterList(context);
        EmitOutput($"← loader entry redirected 0x{exceptionAddress:x} → 0x{_entryPoint:x}");

        if (_breakAtEntry)
        {
            RaiseStopped(BuildStopInfo("breakpoint-hit"));
            if (!WaitForCommand())
                return true;

            ExecutePendingCommand(ref context);
            return true;
        }

        Win32DebugNative.ContinueDebugEvent(_processId, _activeThreadId, Win32DebugNative.DbgContinue);
        return true;
    }

    private void ResolveLoadedEntryPoint(ulong loadedImageBase)
    {
        var target = _debugTarget ?? _executable;
        if (!string.IsNullOrEmpty(target)
            && NativeBinaryEntryPoint.TryResolvePeEntryPoint(target, loadedImageBase, out var entry))
        {
            _entryPoint = entry;
            return;
        }

        if (_entryPoint != 0 && NativeBinaryEntryPoint.TryGetPeEntryRva(target ?? "", out var rva))
            _entryPoint = loadedImageBase + rva;
    }

    private void EnsureEntryBreakpoint()
    {
        if ((_breakAtEntry || _pendingAddresses.Count == 0) && _entryPoint != 0)
            QueueAddress(_entryPoint);
    }

    private void UnplantBreakpointAt(ulong address)
    {
        if (!_breakpoints.TryGetValue(address, out var bp) || !bp.IsPlanted)
            return;

        if (Win32DebugNative.TryWriteMemory(_processHandle, address, [bp.OriginalOpcode]))
            bp.IsPlanted = false;
    }

    private void PlantPendingBreakpoints()
    {
        lock (_sync)
        {
            foreach (var address in _pendingAddresses)
                GetOrCreateBreakpoint(address);
            _pendingAddresses.Clear();
        }

        foreach (var bp in _breakpoints.Values.Where(b => b.IsActive))
            PlantBreakpoint(bp);
    }

    private Win32DebugNative.SoftwareBreakpoint GetOrCreateBreakpoint(ulong address)
    {
        if (_breakpoints.TryGetValue(address, out var existing))
            return existing;

        var created = new Win32DebugNative.SoftwareBreakpoint { Address = address };
        _breakpoints[address] = created;
        return created;
    }

    private bool PlantBreakpoint(Win32DebugNative.SoftwareBreakpoint bp)
    {
        if (_processHandle == 0 || bp.IsPlanted || !bp.IsActive)
            return false;

        var opcode = new byte[1];
        if (!Win32DebugNative.TryReadMemory(_processHandle, bp.Address, opcode))
            return false;

        bp.OriginalOpcode = opcode[0];
        if (!Win32DebugNative.TryWriteMemory(_processHandle, bp.Address, [Win32DebugNative.Int3Opcode]))
        {
            EmitOutput($"← WARN could not plant breakpoint @ 0x{bp.Address:x} err={Marshal.GetLastWin32Error()}");
            return false;
        }

        EmitVerbose($"← planted breakpoint @ 0x{bp.Address:x}");
        bp.IsPlanted = true;
        return true;
    }

    private bool TryReadContext(out Win32DebugNative.Context64 context)
        => TryReadContext(_threadId, out context);

    private bool TryReadContext(uint threadId, out Win32DebugNative.Context64 context, bool debugEventSuspended = false)
    {
        context = new Win32DebugNative.Context64();
        var handle = Win32DebugNative.OpenThread(Win32DebugNative.ThreadAccess, false, threadId);
        if (handle == 0)
            return false;

        try
        {
            return debugEventSuspended
                ? Win32DebugNative.TryGetThreadContextDirect(handle, ref context)
                : Win32DebugNative.TryGetThreadContext(handle, ref context);
        }
        finally
        {
            Win32DebugNative.CloseHandle(handle);
        }
    }

    private bool TryWriteContext(uint threadId, ref Win32DebugNative.Context64 context, bool debugEventSuspended = false)
    {
        var handle = Win32DebugNative.OpenThread(Win32DebugNative.ThreadAccess, false, threadId);
        if (handle == 0)
            return false;

        try
        {
            return debugEventSuspended
                ? Win32DebugNative.TrySetThreadContextDirect(handle, ref context)
                : Win32DebugNative.TrySetThreadContext(handle, ref context);
        }
        finally
        {
            Win32DebugNative.CloseHandle(handle);
        }
    }

    private DebugStopInfo BuildStopInfo(string reason, uint exitCode = 0)
    {
        var rip = _lastContext.Rip != 0 ? _lastContext.Rip : _entryPoint;
        var frame = new DebugStackFrame(
            1,
            reason == "exited" ? "_exit" : "_start",
            1,
            1,
            null,
            $"0x{rip:x}");

        if (reason == "exited")
            frame = frame with { Name = $"exit({exitCode})" };

        return new DebugStopInfo(reason, (int)_threadId, [frame]);
    }

    private void RaiseStopped(DebugStopInfo info) => Stopped?.Invoke(info);

    private void EmitOutput(string line) => OutputReceived?.Invoke(line);

    private void EmitVerbose(string line)
    {
        if (string.Equals(Environment.GetEnvironmentVariable("HLAX64_DEBUG_VERBOSE"), "1", StringComparison.Ordinal))
            EmitOutput(line);
    }

    private void TerminateInferior()
    {
        if (_processId != 0)
        {
            try { Win32DebugNative.DebugActiveProcessStop(_processId); } catch { /* ignore */ }
        }

        if (_processHandle != 0)
        {
            try { Win32DebugNative.TerminateProcess(_processHandle, 1); } catch { /* ignore */ }
            try { Win32DebugNative.CloseHandle(_processHandle); } catch { /* ignore */ }
        }

        if (_threadHandle != 0)
        {
            try { Win32DebugNative.CloseHandle(_threadHandle); } catch { /* ignore */ }
        }

        _processHandle = 0;
        _threadHandle = 0;
        _processId = 0;
        _threadId = 0;
        _activeThreadId = 0;

        if (!string.IsNullOrEmpty(_shadowExecutable))
            DebugProcessCleanup.ReleaseOutputFile(_shadowExecutable);

        if (!string.IsNullOrEmpty(_executable))
            DebugProcessCleanup.ReleaseOutputFile(_executable);

        DebugShadowExecutable.TryDelete(_shadowExecutable);
        _shadowExecutable = null;
    }

    private void Stop()
    {
        _exitRequested = true;
        ClearEphemeralBreakpoints();
        _runToProcessExit = false;
        SignalCommand(Win32DebugNative.DebugCommand.Kill);
        try { _debugThread?.Join(3000); } catch { /* ignore */ }
        _debugThread = null;
        if (_engineAlive)
        {
            TerminateInferior();
            _engineAlive = false;
        }
    }
}
