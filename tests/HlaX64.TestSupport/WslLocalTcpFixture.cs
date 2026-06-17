using System.Diagnostics;
using System.Text;
using HlaX64.Cli.Toolchain;

namespace HlaX64.TestSupport;

/// <summary>
/// TCP fixture that listens inside WSL on 127.0.0.1 so Linux ELF tools can connect locally.
/// </summary>
public sealed class WslLocalTcpFixture : IDisposable
{
    private readonly string _scriptPath;
    private readonly string _wslPortFile;

    public string Host => "127.0.0.1";

    private WslLocalTcpFixture(string scriptPath, string wslPortFile)
    {
        _scriptPath = scriptPath;
        _wslPortFile = wslPortFile;
    }

    public static WslLocalTcpFixture? TryStart(string toolDir)
    {
        var serverPath = Path.Combine(toolDir, "expected.server");
        if (!File.Exists(serverPath))
            return null;

        var template = File.ReadAllText(serverPath).Replace("\r\n", "\n").Replace("`n", "\n");
        var echoMode = template.Trim() == "$ECHO";
        var slowMode = template.Trim() == "$SLOW";
        var noResponseMode = template.Trim() == "$NORESP";

        var scriptPath = Path.Combine(Path.GetTempPath(), $"hlax-wsl-fixture-{Guid.NewGuid():N}.py");
        var wslPortFile = $"/tmp/hlax-port-{Guid.NewGuid():N}";
        File.WriteAllBytes(scriptPath, Encoding.UTF8.GetBytes(BuildPythonScript(template, echoMode, slowMode, noResponseMode).Replace("\r\n", "\n", StringComparison.Ordinal)));

        return new WslLocalTcpFixture(scriptPath, wslPortFile);
    }

    public string BuildCombinedCommand(
        string wslExe,
        string? argumentsPath,
        string repoRoot,
        string outputFile,
        string wslCwd)
    {
        var shellArgs = BuildShellArguments(argumentsPath, repoRoot, outputFile);
        var runnerPath = _scriptPath + ".sh";
        var wslRunner = LinkerTool.ToWslPath(runnerPath);
        var wslScript = LinkerTool.ToWslPath(_scriptPath);
        var wslOutput = LinkerTool.ToWslPath(outputFile);
        var contents = "#!/usr/bin/env bash\n" +
                       "set -eu\n" +
                       "rm -f '" + _wslPortFile + "'\n" +
                       "python3 '" + wslScript + "' '" + _wslPortFile + "' &\n" +
                       "fpid=$!\n" +
                       "for i in $(seq 1 100); do\n" +
                       "  if [ -f '" + _wslPortFile + "' ]; then break; fi\n" +
                       "  sleep 0.05\n" +
                       "done\n" +
                       "port=$(cat '" + _wslPortFile + "')\n" +
                       "cd '" + wslCwd + "'\n" +
                       "'" + wslExe + "' " + shellArgs + " > '" + wslOutput + "'\n" +
                       "cat '" + wslOutput + "'\n" +
                       "status=$?\n" +
                       "kill \"$fpid\" 2>/dev/null || true\n" +
                       "wait \"$fpid\" 2>/dev/null || true\n" +
                       "exit \"$status\"\n";
        File.WriteAllBytes(runnerPath, Encoding.UTF8.GetBytes(contents.Replace("\r\n", "\n", StringComparison.Ordinal)));
        return "bash '" + wslRunner + "'";
    }

    public void WaitForCompletion()
    {
        // The combined shell waits for the fixture process; nothing to do here.
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_scriptPath))
                File.Delete(_scriptPath);
            var runnerPath = _scriptPath + ".sh";
            if (File.Exists(runnerPath))
                File.Delete(runnerPath);
        }
        catch { /* best effort */ }
    }

    private string BuildShellArguments(string? argumentsPath, string repoRoot, string outputFile)
    {
        if (argumentsPath == null || !File.Exists(argumentsPath))
            return string.Empty;

        var tokens = File.ReadAllText(argumentsPath)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (tokens.Length == 0)
            return string.Empty;

        var resolved = new List<string>();
        foreach (var token in tokens)
        {
            if (token == "$OUTPUT")
                resolved.Add(EscapeShell(LinkerTool.ToWslPath(outputFile)));
            else if (token == "$PORT")
                resolved.Add("$port");
            else if (token == "$HOST" || token == "127.0.0.1")
                resolved.Add(EscapeShell(Host));
            else
                resolved.Add(EscapeShell(LinkerTool.ToWslPath(
                    RealToolTestHarness.ResolveRepoRelativeArgumentPublic(token, repoRoot))));
        }

        return string.Join(' ', resolved);
    }

    private static string EscapeShell(string value) => $"'{value.Replace("'", "'\\''")}'";

    private static string BuildPythonScript(string template, bool echoMode, bool slowMode, bool noResponseMode)
    {
        var sb = new StringBuilder();
        sb.AppendLine("import socket, sys, time");
        sb.AppendLine("port_file = sys.argv[1]");
        sb.AppendLine("template = " + QuotePythonString(template));
        sb.AppendLine("echo_mode = " + (echoMode ? "True" : "False"));
        sb.AppendLine("slow_mode = " + (slowMode ? "True" : "False"));
        sb.AppendLine("no_response_mode = " + (noResponseMode ? "True" : "False"));
        sb.AppendLine("""
s = socket.socket()
s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
s.bind(('127.0.0.1', 0))
s.listen(1)
with open(port_file, 'w', encoding='ascii') as f:
    f.write(str(s.getsockname()[1]))
    f.flush()
conn, _ = s.accept()
data = conn.recv(4096)
if slow_mode:
    time.sleep(5)
elif no_response_mode:
    pass
elif echo_mode:
    if data:
        conn.sendall(data)
else:
    conn.sendall(template.encode('ascii'))
conn.close()
s.close()
""");
        return sb.ToString();
    }

    private static string QuotePythonString(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";
}
