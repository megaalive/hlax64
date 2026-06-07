using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace HlaX64.DebugAdapter;

public sealed class GdbBackend : IDisposable
{
    private Process? _gdb;
    private StreamWriter? _stdin;
    private readonly List<string> _breakpoints = [];

    public bool IsAvailable =>
        RuntimeInformation.IsOSPlatform(OSPlatform.Linux) && File.Exists("/usr/bin/gdb");

    public void Launch(string executable, string[]? args = null)
    {
        if (!IsAvailable)
            throw new InvalidOperationException("gdb backend requires Linux with gdb installed.");

        _gdb = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "gdb",
                Arguments = "--interpreter=mi2 --quiet",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        _gdb.Start();
        _stdin = _gdb.StandardInput;
        Write($"file \"{executable}\"");
        foreach (var bp in _breakpoints)
            Write(bp);
        Write("start");
    }

    public void SetBreakpoint(string file, int line)
    {
        var cmd = $"break \"{file}\":{line}";
        _breakpoints.Add(cmd);
        if (_stdin != null)
            Write(cmd);
    }

    public IReadOnlyList<object> GetStackFrames()
    {
        if (_stdin == null) return [new { id = 1, name = "_start", line = 1, column = 1 }];
        Write("-stack-list-frames");
        return
        [
            new { id = 1, name = "_start", line = 1, column = 1, source = new { path = "main.hla64" } }
        ];
    }

    public void Continue() => Write("exec-continue");

    public void Disconnect()
    {
        try { Write("quit"); } catch { /* ignore */ }
        _gdb?.Kill(entireProcessTree: true);
    }

    private void Write(string cmd)
    {
        _stdin?.WriteLine(cmd);
        _stdin?.Flush();
    }

    public void Dispose()
    {
        Disconnect();
        _gdb?.Dispose();
    }
}

public sealed class DebugAdapterHost
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly DapResponseBuilder _responses = new();
    private readonly GdbBackend _gdb = new();
    private bool _running;

    public DebugAdapterHost(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
    }

    public static object Capabilities => new
    {
        supportsConfigurationDoneRequest = true,
        supportsSetVariable = false,
        supportsConditionalBreakpoints = false
    };

    public void Run()
    {
        string? line;
        while ((line = _input.ReadLine()) != null)
        {
            var root = DapJson.Parse(line);
            if (root == null) continue;
            var elem = root.Value;
            if (elem.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "request")
            {
                var req = DapRequest.TryParse(elem);
                if (req != null)
                    HandleRequest(req);
            }
        }
    }

    private void HandleRequest(DapRequest req)
    {
        switch (req.Command)
        {
            case "initialize":
                Write(_responses.Success(req.Seq, "initialize", Capabilities));
                Write(_responses.Event("initialized"));
                break;

            case "launch":
                try
                {
                    var program = req.Arguments?.TryGetProperty("program", out var p) == true
                        ? p.GetString() : null;
                    if (program != null && _gdb.IsAvailable)
                        _gdb.Launch(program);
                    _running = true;
                    Write(_responses.Success(req.Seq, "launch"));
                }
                catch (Exception ex)
                {
                    Write(_responses.Error(req.Seq, "launch", ex.Message));
                }
                break;

            case "configurationDone":
                Write(_responses.Success(req.Seq, "configurationDone"));
                break;

            case "setBreakpoints":
                var file = req.Arguments?.TryGetProperty("source", out var src) == true &&
                           src.TryGetProperty("path", out var pathEl)
                    ? pathEl.GetString() ?? "main.hla64" : "main.hla64";
                var bps = new List<object>();
                if (req.Arguments?.TryGetProperty("breakpoints", out var bpArr) == true)
                {
                    foreach (var bp in bpArr.EnumerateArray())
                    {
                        var ln = bp.TryGetProperty("line", out var lnEl) ? lnEl.GetInt32() : 1;
                        _gdb.SetBreakpoint(file, ln);
                        bps.Add(new { verified = true, id = ln, line = ln });
                    }
                }
                Write(_responses.Success(req.Seq, "setBreakpoints", new { breakpoints = bps }));
                break;

            case "threads":
                Write(_responses.Success(req.Seq, "threads", new
                {
                    threads = new[] { new { id = 1, name = "main" } }
                }));
                break;

            case "stackTrace":
                Write(_responses.Success(req.Seq, "stackTrace", new
                {
                    stackFrames = _gdb.GetStackFrames(),
                    totalFrames = 1
                }));
                break;

            case "scopes":
                Write(_responses.Success(req.Seq, "scopes", new
                {
                    scopes = new[] { new { name = "Locals", variablesReference = 1, expensive = false } }
                }));
                break;

            case "continue":
                _gdb.Continue();
                Write(_responses.Success(req.Seq, "continue", new { allThreadsContinued = true }));
                Write(_responses.Event("continued", new { threadId = 1 }));
                break;

            case "disconnect":
                _gdb.Dispose();
                Write(_responses.Success(req.Seq, "disconnect"));
                break;

            default:
                Write(_responses.Error(req.Seq, req.Command, $"Unsupported command '{req.Command}'"));
                break;
        }
    }

    private void Write(object payload)
    {
        _output.WriteLine(DapJson.Serialize(payload));
        _output.Flush();
    }
}
