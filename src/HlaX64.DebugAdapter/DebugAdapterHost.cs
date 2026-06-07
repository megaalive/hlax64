using System.Text.Json;

namespace HlaX64.DebugAdapter;

public sealed class DebugAdapterHost
{
    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly DapResponseBuilder _responses = new();
    private readonly IDebugBackend _backend;

    public DebugAdapterHost(TextReader input, TextWriter output, IDebugBackend? backend = null)
    {
        _input = input;
        _output = output;
        _backend = backend ?? DebugBackendFactory.CreateDefault();
        _backend.Stopped += OnBackendStopped;
        _backend.OutputReceived += line => Write(_responses.Event("output", new
        {
            category = "console",
            output = line + Environment.NewLine
        }));
    }

    public IDebugBackend Backend => _backend;

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

    private void OnBackendStopped(DebugStopInfo info)
    {
        Write(_responses.Event("stopped", new
        {
            reason = info.Reason,
            threadId = info.ThreadId,
            allThreadsStopped = true
        }));
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
                    if (program != null && _backend.IsAvailable)
                        _backend.Launch(program);
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
                        _backend.SetBreakpoint(file, ln);
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
                var frames = _backend.GetStackFrames();
                Write(_responses.Success(req.Seq, "stackTrace", new
                {
                    stackFrames = frames,
                    totalFrames = frames.Count
                }));
                break;

            case "scopes":
                Write(_responses.Success(req.Seq, "scopes", new
                {
                    scopes = new[]
                    {
                        new { name = "Locals", variablesReference = 1, expensive = false },
                        new { name = "Registers", variablesReference = 2, expensive = false }
                    }
                }));
                break;

            case "variables":
                var variablesReference = req.Arguments?.TryGetProperty("variablesReference", out var vr) == true
                    ? vr.GetInt32() : 0;
                if (variablesReference == 2)
                {
                    var regs = _backend.GetRegisters()
                        .Select(r => new { name = r.Name, value = r.Value, variablesReference = 0 })
                        .ToArray();
                    Write(_responses.Success(req.Seq, "variables", new { variables = regs }));
                }
                else
                {
                    Write(_responses.Success(req.Seq, "variables", new { variables = Array.Empty<object>() }));
                }
                break;

            case "continue":
                _backend.Continue();
                Write(_responses.Success(req.Seq, "continue", new { allThreadsContinued = true }));
                Write(_responses.Event("continued", new { threadId = 1 }));
                break;

            case "next":
                _backend.StepOver();
                Write(_responses.Success(req.Seq, "next", new { allThreadsContinued = true }));
                break;

            case "stepIn":
                _backend.StepInto();
                Write(_responses.Success(req.Seq, "stepIn", new { allThreadsContinued = true }));
                break;

            case "stepOut":
                _backend.StepOut();
                Write(_responses.Success(req.Seq, "stepOut", new { allThreadsContinued = true }));
                break;

            case "disconnect":
                _backend.Dispose();
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
