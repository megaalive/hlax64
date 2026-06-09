namespace HlaX64.AssemblyLab.Services;

public interface ILabTerminalHost
{
    void Configure(string workingDirectory, string? repoRoot);

    void SendLine(string line);

    void FocusTerminal();

    void NotifyTabVisible();

    void Restart();

    void StopShell();
}
