using System.Text.RegularExpressions;

namespace HlaX64.DebugAdapter;

public sealed record DebugStackFrame(int Id, string Name, int Line, int Column, string? FilePath, string? Address = null);

public sealed record DebugRegister(string Name, string Value);

public sealed record DebugStopInfo(string Reason, int ThreadId, IReadOnlyList<DebugStackFrame> Frames);

/// <summary>Parses gdb MI2 and lldb text output into structured debug events.</summary>
public static partial class MiOutputParser
{
    public static bool TryParseGdbStopped(string line, out DebugStopInfo? info)
    {
        info = null;
        if (!line.Contains("*stopped", StringComparison.Ordinal))
            return false;

        var reason = MatchReason().Match(line).Groups[1].Value;
        if (string.IsNullOrEmpty(reason))
            reason = "unknown";

        var threadId = 1;
        var threadMatch = MatchThreadId().Match(line);
        if (threadMatch.Success && int.TryParse(threadMatch.Groups[1].Value, out var tid))
            threadId = tid;

        var frames = ParseGdbFramesFromStopped(line);
        info = new DebugStopInfo(reason, threadId, frames);
        return true;
    }

    public static IReadOnlyList<DebugStackFrame> ParseGdbStackListFrames(string miResponse)
    {
        var frames = new List<DebugStackFrame>();
        foreach (Match m in MatchGdbFrame().Matches(miResponse))
        {
            var level = int.TryParse(m.Groups["level"].Value, out var lv) ? lv : frames.Count;
            var func = Unescape(m.Groups["func"].Value);
            var file = m.Groups["file"].Success ? Unescape(m.Groups["file"].Value) : null;
            var line = m.Groups["line"].Success && int.TryParse(m.Groups["line"].Value, out var ln) ? ln : 1;
            frames.Add(new DebugStackFrame(level + 1, func, line, 1, file));
        }

        return frames;
    }

    public static IReadOnlyList<DebugRegister> ParseGdbRegisterValues(string miResponse)
    {
        var regs = new List<DebugRegister>();
        foreach (Match m in MatchGdbRegister().Matches(miResponse))
        {
            var name = m.Groups["name"].Success ? m.Groups["name"].Value : null;
            var value = m.Groups["value"].Value;
            if (name != null)
                regs.Add(new DebugRegister(name, value));
        }

        if (regs.Count > 0)
            return regs;

        var numbers = MatchGdbRegisterNumberOnly().Matches(miResponse).Cast<Match>().ToList();
        if (numbers.Count == 0)
            return regs;

        var names = DefaultRegisterNames();
        for (int i = 0; i < numbers.Count && i < names.Count; i++)
            regs.Add(new DebugRegister(names[i], numbers[i].Groups["value"].Value));

        return regs;
    }

    public static string? ParseGdbEvaluateExpression(string miResponse)
    {
        var match = MatchEvaluateValue().Match(miResponse);
        return match.Success ? match.Groups["value"].Value : null;
    }

    public static string? ParseGdbStoppedAddress(string line)
    {
        var match = MatchGdbStoppedAddr().Match(line);
        return match.Success ? match.Groups["addr"].Value : null;
    }

    private static IReadOnlyList<string> DefaultRegisterNames() =>
    [
        "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "rbp", "rsp",
        "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15", "rip", "eflags"
    ];

    public static bool TryParseLldbStopped(string line, out DebugStopInfo? info)
    {
        info = null;
        var isStopLine = line.Contains("stop reason", StringComparison.OrdinalIgnoreCase)
            || (line.Contains("Process ", StringComparison.Ordinal) && line.Contains(" stopped", StringComparison.Ordinal))
            || line.Contains("hit program breakpoint", StringComparison.OrdinalIgnoreCase);
        if (!isStopLine)
            return false;

        var reason = MatchLldbReason().Match(line).Groups[1].Value.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            if (line.Contains("breakpoint", StringComparison.OrdinalIgnoreCase))
                reason = "breakpoint";
            else if (line.Contains("exited", StringComparison.OrdinalIgnoreCase))
                reason = "exited";
            else
                reason = "stopped";
        }

        var frames = new List<DebugStackFrame>();
        var frameMatch = MatchLldbFrame().Match(line);
        if (frameMatch.Success)
        {
            var func = frameMatch.Groups["func"].Value;
            var file = frameMatch.Groups["file"].Success ? frameMatch.Groups["file"].Value : null;
            var lineNo = frameMatch.Groups["line"].Success && int.TryParse(frameMatch.Groups["line"].Value, out var ln)
                ? ln : 1;
            frames.Add(new DebugStackFrame(1, func, lineNo, 1, file));
        }
        else
        {
            frames.Add(new DebugStackFrame(1, "main", 1, 1, null));
        }

        info = new DebugStopInfo(reason, 1, frames);
        return true;
    }

    public static IReadOnlyList<DebugStackFrame> ParseLldbBacktrace(IEnumerable<string> lines)
    {
        var frames = new List<DebugStackFrame>();
        var id = 1;
        foreach (var line in lines)
        {
            var m = MatchLldbBacktraceFrame().Match(line);
            if (!m.Success) continue;
            var func = m.Groups["func"].Value;
            var file = m.Groups["file"].Success ? m.Groups["file"].Value : null;
            var lineNo = m.Groups["line"].Success && int.TryParse(m.Groups["line"].Value, out var ln) ? ln : 1;
            frames.Add(new DebugStackFrame(id++, func, lineNo, 1, file));
        }

        return frames;
    }

    public static IReadOnlyList<DebugRegister> ParseLldbRegisterDump(IEnumerable<string> lines)
    {
        var regs = new List<DebugRegister>();
        foreach (var line in lines)
        {
            var m = MatchLldbRegister().Match(line);
            if (!m.Success) continue;
            regs.Add(new DebugRegister(m.Groups["name"].Value, m.Groups["value"].Value.Trim()));
        }

        return regs;
    }

    private static IReadOnlyList<DebugStackFrame> ParseGdbFramesFromStopped(string line)
    {
        var frameMatch = MatchGdbStoppedFrame().Match(line);
        if (!frameMatch.Success)
            return [new DebugStackFrame(1, "_start", 1, 1, null)];

        var func = Unescape(frameMatch.Groups["func"].Value);
        var file = frameMatch.Groups["file"].Success ? Unescape(frameMatch.Groups["file"].Value) : null;
        var addr = frameMatch.Groups["addr"].Success ? frameMatch.Groups["addr"].Value : null;
        var lineNo = frameMatch.Groups["line"].Success && int.TryParse(frameMatch.Groups["line"].Value, out var ln)
            ? ln : 1;
        return [new DebugStackFrame(1, func, lineNo, 1, file, addr)];
    }

    private static string Unescape(string value) =>
        value.Replace("\\\"", "\"", StringComparison.Ordinal);

    [GeneratedRegex(@"reason=""([^""]+)""")]
    private static partial Regex MatchReason();

    [GeneratedRegex(@"thread-id=""(\d+)""")]
    private static partial Regex MatchThreadId();

    [GeneratedRegex(@"frame=\{[^}]*addr=""(?<addr>[^""]+)""[^}]*func=""(?<func>[^""]+)""(?:,[^}]*file=""(?<file>[^""]+)"")?(?:,[^}]*line=""(?<line>\d+)"")?")]
    private static partial Regex MatchGdbStoppedFrame();

    [GeneratedRegex(@"\{number=""(?<num>\d+)"",value=""(?<value>[^""]+)""\}")]
    private static partial Regex MatchGdbRegisterNumberOnly();

    [GeneratedRegex(@",value=""(?<value>[^""]+)""\}")]
    private static partial Regex MatchEvaluateValue();

    [GeneratedRegex(@"addr=""(?<addr>0x[^""]+)""")]
    private static partial Regex MatchGdbStoppedAddr();

    [GeneratedRegex(@"frame=\{level=""(?<level>\d+)"",[^}]*func=""(?<func>[^""]+)""(?:,[^}]*file=""(?<file>[^""]+)"")?(?:,[^}]*line=""(?<line>\d+)"")?")]
    private static partial Regex MatchGdbFrame();

    [GeneratedRegex(@"\{number=""(?<num>\d+)"",name=""(?<name>[^""]+)"",value=""(?<value>[^""]+)""\}")]
    private static partial Regex MatchGdbRegister();

    [GeneratedRegex(@"stop reason = (?<reason>[^\n\r]+)", RegexOptions.IgnoreCase)]
    private static partial Regex MatchLldbReason();

    [GeneratedRegex(@"frame #0: .*`(?<func>[^']+)'(?: at (?<file>[^:]+):(?<line>\d+))?")]
    private static partial Regex MatchLldbFrame();

    [GeneratedRegex(@"^\s*frame #(?<id>\d+): .*`(?<func>[^']+)'(?: at (?<file>[^:]+):(?<line>\d+))?", RegexOptions.Multiline)]
    private static partial Regex MatchLldbBacktraceFrame();

    [GeneratedRegex(@"^\s*(?<name>[a-z]+)\s*=\s*(?<value>.+)$", RegexOptions.IgnoreCase)]
    private static partial Regex MatchLldbRegister();
}
