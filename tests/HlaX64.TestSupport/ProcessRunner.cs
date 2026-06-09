using System.Diagnostics;
using System.Text;

namespace HlaX64.TestSupport;

public static class ProcessRunner
{
    public sealed record ProcessResult(int ExitCode, string Stdout, string Stderr, bool TimedOut);

    public static ProcessResult Run(ProcessStartInfo startInfo, TimeSpan timeout, string? stdin = null)
    {
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        if (stdin != null)
            startInfo.RedirectStandardInput = true;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Failed to start process: {startInfo.FileName}");

        var stdoutBuilder = new StringBuilder();
        var stderrBuilder = new StringBuilder();
        var stdoutDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrDone = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) stdoutDone.TrySetResult();
            else stdoutBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) stderrDone.TrySetResult();
            else stderrBuilder.AppendLine(e.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        if (stdin != null)
        {
            process.StandardInput.Write(stdin);
            process.StandardInput.Close();
        }

        if (!process.WaitForExit((int)timeout.TotalMilliseconds))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            return new ProcessResult(-1, stdoutBuilder.ToString(), stderrBuilder.ToString(), TimedOut: true);
        }

        Task.WaitAll([stdoutDone.Task, stderrDone.Task], TimeSpan.FromSeconds(5));
        return new ProcessResult(process.ExitCode, stdoutBuilder.ToString(), stderrBuilder.ToString(), TimedOut: false);
    }
}
