namespace HlaX64.DebugAdapter;

public sealed class LldbBackend : DebugEngineSession
{
    private readonly List<string> _breakpoints = [];
    private readonly List<string> _lldbLines = [];
    private IReadOnlyList<DebugStackFrame> _cachedFrames = [];
    private IReadOnlyList<DebugRegister> _cachedRegisters = [];

    public override string Name => "lldb";
    public override bool IsAvailable => DebuggerProbe.IsLldbUsable(out _);

    public static bool TryFindLldb(out string path) => DebuggerProbe.TryFindLldb(out path);

    public override void Launch(string executable, string[]? args = null)
    {
        if (!TryFindLldb(out var lldbPath))
            throw new InvalidOperationException(
                "lldb backend requires Windows with lldb on PATH or in Program Files/LLVM/bin.");

        StartEngine(lldbPath, "-Q");
        WriteLine($"target create \"{executable}\"");
        foreach (var bp in _breakpoints)
            WriteLine(bp);
        if (_breakpoints.Count == 0)
            WriteLine("breakpoint set --name _start");
        WriteLine("run");
    }

    public override void SetBreakpoint(string file, int line)
    {
        if (file.EndsWith(".hla64", StringComparison.OrdinalIgnoreCase))
        {
            SetBreakpointBySymbol("_start");
            return;
        }

        var cmd = $"breakpoint set -f \"{file}\" -l {line}";
        _breakpoints.Add(cmd);
        if (Stdin != null)
            WriteLine(cmd);
    }

    public override void SetBreakpointBySymbol(string symbol)
    {
        var cmd = $"breakpoint set --name {symbol}";
        _breakpoints.Add(cmd);
        if (Stdin != null)
            WriteLine(cmd);
    }

    public override void SetBreakpointAtAddress(string symbol, int byteOffset)
    {
        var cmd = byteOffset <= 0
            ? $"breakpoint set --name {symbol}"
            : $"breakpoint set -a \"({symbol}+{byteOffset})\"";
        _breakpoints.Add(cmd);
        if (Stdin != null)
            WriteLine(cmd);
    }

    public override void Continue() => WriteLine("process continue");

    public override void StepOver() => WriteLine("thread step-over");

    public override void StepInto() => WriteLine("thread step-in");

    public override void StepOut() => WriteLine("thread step-out");

    public override void Kill() => WriteLine("process kill");

    protected override void HandleEngineLine(string line)
    {
        _lldbLines.Add(line);
        if (_lldbLines.Count > 512)
            _lldbLines.RemoveRange(0, 256);

        if (MiOutputParser.TryParseLldbStopped(line, out var info) && info != null)
        {
            _cachedFrames = info.Frames;
            RaiseStopped(info);
        }
    }

    protected override IReadOnlyList<DebugStackFrame> QueryStackFrames()
    {
        if (!IsEngineAlive)
            return _cachedFrames.Count > 0
                ? _cachedFrames
                : [new DebugStackFrame(1, "_start", 1, 1, null)];

        if (!TryWriteLine("thread backtrace"))
            return _cachedFrames.Count > 0
                ? _cachedFrames
                : [new DebugStackFrame(1, "_start", 1, 1, null)];

        Thread.Sleep(250);
        var parsed = MiOutputParser.ParseLldbBacktrace(_lldbLines);
        if (parsed.Count > 0)
        {
            _cachedFrames = parsed;
            return parsed;
        }

        return _cachedFrames.Count > 0
            ? _cachedFrames
            : [new DebugStackFrame(1, "main", 1, 1, null)];
    }

    protected override IReadOnlyList<DebugRegister> QueryRegisters()
    {
        if (!IsEngineAlive)
            return _cachedRegisters;

        if (!TryWriteLine("register read rax rbx rcx rdx rsi rdi rbp rsp r8 r9 r10 r11 r12 r13 r14 r15 rip eflags"))
            return _cachedRegisters;

        Thread.Sleep(250);
        var parsed = MiOutputParser.ParseLldbRegisterDump(_lldbLines);
        if (parsed.Count > 0)
        {
            _cachedRegisters = parsed;
            return parsed;
        }

        return _cachedRegisters;
    }
}
