using HlaX64.Compiler.Debug;

namespace HlaX64.AssemblyLab.Services;

public sealed record ResolvedDebugBreakpoint(
    string Kind,
    string? FilePath,
    int Line,
    string? Symbol);

/// <summary>Maps HlaX64 source breakpoints to symbols/lines the native debugger can use.</summary>
public static class DebugBreakpointResolver
{
    public static IReadOnlyList<ResolvedDebugBreakpoint> Resolve(
        IEnumerable<int> sourceLines,
        string sourcePath,
        string? nasmPath,
        SourceMapDocument? sourceMap)
    {
        var resolved = new List<ResolvedDebugBreakpoint>();
        foreach (var line in sourceLines.OrderBy(x => x))
        {
            if (line <= 0)
                continue;

            var nasmLine = sourceMap?.LookupBySource(line)?.NasmLine;
            if (nasmLine != null && !string.IsNullOrWhiteSpace(nasmPath) && File.Exists(nasmPath))
            {
                resolved.Add(new ResolvedDebugBreakpoint(
                    "nasm-line",
                    Path.GetFullPath(nasmPath),
                    nasmLine.Value,
                    null));
                continue;
            }

            // PE/ELF binaries built from NASM usually expose _start, not .hla64 debug info.
            resolved.Add(new ResolvedDebugBreakpoint(
                "symbol",
                null,
                line,
                "_start"));
        }

        return resolved;
    }
}
