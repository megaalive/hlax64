namespace HlaX64.AssemblyLab.Models;

public sealed record DebugArgumentsRequest(
    string ProgramName,
    string DefaultArguments,
    string Hint);
