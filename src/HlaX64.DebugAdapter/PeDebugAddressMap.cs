using System.Globalization;
using HlaX64.Compiler.Debug;

namespace HlaX64.DebugAdapter;

/// <summary>Maps linked PE instruction addresses to HLA source lines via NASM + source map.</summary>
public static class PeDebugAddressMap
{
    public sealed record CachedMaps(
        IReadOnlyDictionary<ulong, int> SourceByAddress,
        IReadOnlyDictionary<ulong, int> NasmByAddress,
        ulong UserCodeEnd);

    private sealed record MapCacheKey(string Exe, string Nasm, long ExeTicks, long NasmTicks, string? SourcePath);

    private static CachedMaps? _cachedMaps;
    private static MapCacheKey? _cacheKey;

    public static void InvalidateCache()
    {
        _cachedMaps = null;
        _cacheKey = null;
        _irSequenceCache = null;
    }

    public static CachedMaps GetOrBuild(string executablePath, string nasmPath, SourceMapDocument? sourceMap)
    {
        var key = new MapCacheKey(
            executablePath,
            nasmPath,
            File.GetLastWriteTimeUtc(executablePath).Ticks,
            File.GetLastWriteTimeUtc(nasmPath).Ticks,
            sourceMap?.Source);

        if (_cacheKey != null && _cacheKey.Equals(key) && _cachedMaps != null)
            return _cachedMaps;

        var sourceByAddress = BuildSourceLinesByAddress(executablePath, nasmPath, sourceMap);
        var nasmByAddress = BuildNasmLinesByAddress(executablePath, nasmPath);
        var userCodeEnd = ComputeUserCodeEnd(executablePath, nasmByAddress);

        _cacheKey = key;
        _cachedMaps = new CachedMaps(sourceByAddress, nasmByAddress, userCodeEnd);
        return _cachedMaps;
    }

    public static IReadOnlyDictionary<ulong, int> BuildSourceLinesByAddress(
        string executablePath,
        string nasmPath,
        SourceMapDocument? sourceMap)
    {
        var map = new Dictionary<ulong, int>();
        if (string.IsNullOrWhiteSpace(executablePath)
            || !File.Exists(executablePath)
            || string.IsNullOrWhiteSpace(nasmPath)
            || !File.Exists(nasmPath))
        {
            return map;
        }

        var beginSourceLine = FindBeginSourceLine(sourceMap);
        var instructionLines = EnumerateInstructionLines(nasmPath).ToList();
        if (instructionLines.Count == 0)
            return map;

        var firstNasmLine = instructionLines[0];

        foreach (var nasmLine in instructionLines)
        {
            if (NasmLineAddressResolver.TryResolveUserInstruction(executablePath, nasmPath, nasmLine) is not ulong address)
                continue;

            var sourceLine = ResolveSourceLine(sourceMap, nasmPath, nasmLine, firstNasmLine, beginSourceLine);
            if (sourceLine > 0)
                map[address] = sourceLine;
        }

        return map;
    }

    public static IReadOnlyDictionary<ulong, int> BuildNasmLinesByAddress(
        string executablePath,
        string nasmPath)
    {
        var map = new Dictionary<ulong, int>();
        if (string.IsNullOrWhiteSpace(executablePath)
            || !File.Exists(executablePath)
            || string.IsNullOrWhiteSpace(nasmPath)
            || !File.Exists(nasmPath))
        {
            return map;
        }

        foreach (var nasmLine in EnumerateInstructionLines(nasmPath))
        {
            if (NasmLineAddressResolver.TryResolveUserInstruction(executablePath, nasmPath, nasmLine) is ulong address)
                map[address] = nasmLine;
        }

        return map;
    }

    public static bool IsAddressInMainModule(ulong rip, string executablePath, string nasmPath)
    {
        if (!NativeBinaryEntryPoint.TryGetEntryPoint(executablePath, out var entry))
            return false;

        if (rip < entry)
            return false;

        if (NativeBinaryEntryPoint.TryGetPePreferredImageBase(executablePath, out var imageBase)
            && NativeBinaryEntryPoint.TryGetPeSizeOfImage(executablePath, out var sizeOfImage)
            && sizeOfImage > 0)
        {
            return rip >= imageBase && rip < imageBase + sizeOfImage;
        }

        if (TryGetUserCodeEnd(executablePath, nasmPath, out var end))
            return rip <= end;

        return false;
    }

    public static bool IsUserCodeAddress(ulong rip, string executablePath, string nasmPath)
    {
        if (!NativeBinaryEntryPoint.TryGetEntryPoint(executablePath, out var entry))
            return false;

        if (rip < entry)
            return false;

        if (!TryGetUserCodeEnd(executablePath, nasmPath, out var userEnd))
        {
            var maps = BuildNasmLinesByAddress(executablePath, nasmPath);
            userEnd = ComputeUserCodeEnd(executablePath, maps);
            if (userEnd <= entry)
                return false;
        }

        return rip <= userEnd;
    }

    public static bool IsUserCodeAddress(ulong rip, CachedMaps maps, string executablePath, string nasmPath)
    {
        if (maps.UserCodeEnd != 0)
            return rip <= maps.UserCodeEnd;

        return IsUserCodeAddress(rip, executablePath, nasmPath);
    }

    public static int? LookupCallSiteSourceLine(
        ulong returnAddress,
        IReadOnlyDictionary<ulong, int> sourceByAddress)
    {
        if (returnAddress == 0 || sourceByAddress.Count == 0)
            return null;

        for (var back = 2u; back <= 7; back++)
        {
            var callAddress = returnAddress - back;
            if (sourceByAddress.TryGetValue(callAddress, out var exact))
                return exact;
        }

        ulong? best = null;
        foreach (var addr in sourceByAddress.Keys)
        {
            if (addr < returnAddress && (best == null || addr > best))
                best = addr;
        }

        return best != null && sourceByAddress.TryGetValue(best.Value, out var line) ? line : null;
    }

    /// <summary>True when <paramref name="returnAddress"/> sits immediately after a mapped instruction (typically a call).</summary>
    public static bool IsPlausibleCallReturnAddress(
        ulong returnAddress,
        IReadOnlyDictionary<ulong, int> nasmByAddress)
    {
        if (returnAddress == 0 || nasmByAddress.Count == 0)
            return false;

        for (var back = 2u; back <= 7; back++)
        {
            if (nasmByAddress.ContainsKey(returnAddress - back))
                return true;
        }

        return false;
    }

    public delegate bool TryReadStackQword(ulong address, out ulong value);

    /// <summary>Finds the innermost plausible return address on the stack (skips saved registers).</summary>
    public static bool TryFindStackReturnAddress(
        ulong rsp,
        TryReadStackQword readSlot,
        string executablePath,
        string nasmPath,
        CachedMaps maps,
        out ulong returnAddress)
    {
        returnAddress = 0;
        for (var slot = 0; slot < 64; slot++)
        {
            var slotAddress = rsp + (ulong)(slot * 8);
            if (!readSlot(slotAddress, out var candidate) || candidate == 0)
                continue;

            if (!IsAddressInMainModule(candidate, executablePath, nasmPath))
                continue;

            if (!IsPlausibleCallReturnAddress(candidate, maps.NasmByAddress))
                continue;

            returnAddress = candidate;
            return true;
        }

        return false;
    }

    public static bool IsProgramShutdownPhase(
        ulong rip,
        string executablePath,
        string nasmPath,
        CachedMaps maps,
        int? callSiteLineFromStack)
    {
        if (IsUserCodeAddress(rip, maps, executablePath, nasmPath))
            return false;

        if (!IsAddressInMainModule(rip, executablePath, nasmPath))
            return true;

        return callSiteLineFromStack is null or 0;
    }

    public static bool IsExitJumpTarget(
        ulong target,
        string executablePath,
        string nasmPath,
        CachedMaps maps)
    {
        if (!IsAddressInMainModule(target, executablePath, nasmPath))
            return false;

        if (IsUserCodeAddress(target, maps, executablePath, nasmPath))
            return false;

        return maps.UserCodeEnd > 0 && target > maps.UserCodeEnd + 0x300;
    }

    public static int? LookupSourceLine(ulong rip, IReadOnlyDictionary<ulong, int> sourceByAddress)
    {
        if (sourceByAddress.Count == 0)
            return null;

        if (sourceByAddress.TryGetValue(rip, out var exact))
            return exact;

        ulong? best = null;
        foreach (var addr in sourceByAddress.Keys)
        {
            if (addr <= rip && (best == null || addr > best))
                best = addr;
        }

        return best != null && sourceByAddress.TryGetValue(best.Value, out var line) ? line : null;
    }

    public static int? LookupNasmLine(ulong rip, IReadOnlyDictionary<ulong, int> nasmByAddress)
        => LookupSourceLine(rip, nasmByAddress);

    public static int ResolveSourceLineForNasm(
        SourceMapDocument? sourceMap,
        string nasmPath,
        int nasmLine)
    {
        var instructionLines = EnumerateInstructionLines(nasmPath).ToList();
        if (instructionLines.Count == 0)
            return 0;

        return ResolveSourceLine(
            sourceMap,
            nasmPath,
            nasmLine,
            instructionLines[0],
            FindBeginSourceLine(sourceMap));
    }

    public static bool IsTrustedSourceMapNasmLine(SourceMapDocument? sourceMap, int nasmLine)
        => !HasCollidingNasmLineEntries(sourceMap, nasmLine);

    private static int ResolveSourceLine(
        SourceMapDocument? sourceMap,
        string nasmPath,
        int nasmLine,
        int firstNasmLine,
        int beginSourceLine)
    {
        var sourcePath = sourceMap?.Source;

        if (beginSourceLine > 0
            && TryGetInstructionIndex(nasmPath, nasmLine) is int instIndex
            && instIndex < 3)
        {
            if (instIndex == 0)
                return beginSourceLine;

            var steppable = LoadSteppableSourceLines(sourcePath);
            if (instIndex - 1 < steppable.Count)
                return steppable[instIndex - 1];

            return SnapToMeaningfulSourceLine(sourcePath, beginSourceLine + instIndex);
        }

        if (TryGetIrIdNearNasmLine(nasmPath, nasmLine) is int irId)
        {
            var fromIr = sourceMap?.LookupByIrId(irId)?.SourceLine;
            if (fromIr is > 0)
                return SnapToMeaningfulSourceLine(sourcePath, fromIr.Value);

            if (TryResolveFromIrSequence(sourcePath, nasmPath, irId) is int fromSequence and > 0)
                return SnapToMeaningfulSourceLine(sourcePath, fromSequence);
        }
        else if (beginSourceLine > 0 && firstNasmLine > 0 && nasmLine >= firstNasmLine)
        {
            return SnapToMeaningfulSourceLine(
                sourcePath,
                beginSourceLine + (nasmLine - firstNasmLine));
        }

        if (!IsLabelOnlyNasmLine(nasmPath, nasmLine)
            && !HasCollidingNasmLineEntries(sourceMap, nasmLine))
        {
            var mapped = sourceMap?.LookupByNasmLine(nasmLine);
            if (mapped != null)
                return SnapToMeaningfulSourceLine(sourcePath, mapped.SourceLine);
        }

        return 0;
    }

    private static int? TryGetInstructionIndex(string nasmPath, int nasmLine)
    {
        var index = 0;
        foreach (var line in EnumerateInstructionLines(nasmPath))
        {
            if (line == nasmLine)
                return index;
            index++;
        }

        return null;
    }

    private sealed record IrSequenceCache(
        string NasmPath,
        long NasmTicks,
        string? SourcePath,
        long SourceTicks,
        List<(int IrId, int SourceLine)> IrAnchors);

    private static IrSequenceCache? _irSequenceCache;

    private static int? TryResolveFromIrSequence(string? sourcePath, string nasmPath, int irId)
    {
        var cache = GetIrSequenceCache(sourcePath, nasmPath);
        if (cache == null || cache.IrAnchors.Count == 0)
            return null;

        for (var i = cache.IrAnchors.Count - 1; i >= 0; i--)
        {
            if (cache.IrAnchors[i].IrId == irId)
                return cache.IrAnchors[i].SourceLine;
        }

        return null;
    }

    private static IrSequenceCache? GetIrSequenceCache(string? sourcePath, string nasmPath)
    {
        if (!File.Exists(nasmPath))
            return null;

        var nasmTicks = File.GetLastWriteTimeUtc(nasmPath).Ticks;
        var sourceTicks = !string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath)
            ? File.GetLastWriteTimeUtc(sourcePath).Ticks
            : 0;

        if (_irSequenceCache != null
            && _irSequenceCache.NasmPath == nasmPath
            && _irSequenceCache.NasmTicks == nasmTicks
            && _irSequenceCache.SourcePath == sourcePath
            && _irSequenceCache.SourceTicks == sourceTicks)
        {
            return _irSequenceCache;
        }

        _irSequenceCache = new IrSequenceCache(
            nasmPath,
            nasmTicks,
            sourcePath,
            sourceTicks,
            BuildIrAnchors(sourcePath, nasmPath));
        return _irSequenceCache;
    }

    private static List<(int IrId, int SourceLine)> BuildIrAnchors(string? sourcePath, string nasmPath)
    {
        var anchors = new List<(int, int)>();
        var steppable = LoadSteppableSourceLines(sourcePath);
        if (steppable.Count == 0)
            return anchors;

        var nasmLines = File.ReadAllLines(nasmPath);
        var stepIndex = 0;
        var lastSourceLine = 0;

        for (var i = 0; i < nasmLines.Length; i++)
        {
            var trimmed = nasmLines[i].Trim();
            if (!trimmed.StartsWith("; ir:", StringComparison.Ordinal))
                continue;

            var rest = trimmed["; ir:".Length..].Trim();
            if (!int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out var irId))
                continue;

            var nextInstr = FindNextInstructionLine(nasmLines, i + 1);
            var sourceLine = lastSourceLine;
            if (!IsBranchOnlyInstruction(nasmLines, nextInstr)
                && stepIndex < steppable.Count)
            {
                sourceLine = steppable[stepIndex];
                stepIndex++;
                lastSourceLine = sourceLine;
            }
            else if (lastSourceLine > 0)
            {
                sourceLine = lastSourceLine;
            }

            if (sourceLine > 0)
                anchors.Add((irId, sourceLine));
        }

        return anchors;
    }

    private static int FindNextInstructionLine(string[] nasmLines, int startIndex)
    {
        for (var i = startIndex; i < nasmLines.Length; i++)
        {
            if (NasmLineClassifier.IsInstructionLine(nasmLines[i]))
                return i + 1;
        }

        return -1;
    }

    private static bool IsBranchOnlyInstruction(string[] nasmLines, int nasmLine)
    {
        if (nasmLine <= 0 || nasmLine > nasmLines.Length)
            return false;

        var trimmed = nasmLines[nasmLine - 1].Trim().Split(';')[0].Trim();
        if (trimmed.Length == 0)
            return false;

        var op = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        return op.Equals("jl", StringComparison.OrdinalIgnoreCase)
               || op.Equals("jg", StringComparison.OrdinalIgnoreCase)
               || op.Equals("je", StringComparison.OrdinalIgnoreCase)
               || op.Equals("jne", StringComparison.OrdinalIgnoreCase)
               || op.Equals("jmp", StringComparison.OrdinalIgnoreCase);
    }

    private static List<int> LoadSteppableSourceLines(string? sourcePath)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return list;

        var lines = File.ReadAllLines(sourcePath);
        var beginLine = 0;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].TrimStart().StartsWith("begin ", StringComparison.OrdinalIgnoreCase))
            {
                beginLine = i + 1;
                break;
            }
        }

        var endLine = FindEndSourceLine(lines);
        if (beginLine <= 0 || endLine <= beginLine)
            return list;

        for (var line = beginLine + 1; line < endLine; line++)
        {
            if (IsSteppableSourceLine(lines[line - 1]))
                list.Add(line);
        }

        return list;
    }

    private static int? TryGetIrIdNearNasmLine(string nasmPath, int nasmLine)
    {
        if (nasmLine <= 0 || !File.Exists(nasmPath))
            return null;

        var lines = File.ReadAllLines(nasmPath);
        for (var probe = nasmLine; probe >= Math.Max(1, nasmLine - 4); probe--)
        {
            if (probe > lines.Length)
                continue;

            var trimmed = lines[probe - 1].Trim();
            if (!trimmed.StartsWith("; ir:", StringComparison.Ordinal))
                continue;

            var rest = trimmed["; ir:".Length..].Trim();
            if (int.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out var irId))
                return irId;
        }

        return null;
    }

    private static bool HasCollidingNasmLineEntries(SourceMapDocument? sourceMap, int nasmLine)
    {
        if (sourceMap == null)
            return false;

        var count = 0;
        foreach (var entry in sourceMap.Entries)
        {
            if (entry.NasmLine == nasmLine)
                count++;
            if (count > 1)
                return true;
        }

        return false;
    }

    private static int SnapToMeaningfulSourceLine(string? sourcePath, int line)
    {
        if (line <= 0 || string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return line;

        var lines = File.ReadAllLines(sourcePath);
        var endLine = FindEndSourceLine(lines);
        if (endLine > 0 && line >= endLine)
            line = endLine - 1;

        if (line > lines.Length)
        {
            for (var probe = lines.Length; probe >= 1; probe--)
            {
                if (IsSteppableSourceLine(lines[probe - 1]))
                    return probe;
            }

            return lines.Length;
        }

        if (IsSteppableSourceLine(lines[line - 1]))
            return line;

        for (var probe = line - 1; probe >= 1; probe--)
        {
            if (IsSteppableSourceLine(lines[probe - 1]))
                return probe;
        }

        for (var probe = line + 1; probe <= lines.Length; probe++)
        {
            if (IsSteppableSourceLine(lines[probe - 1]))
                return probe;
        }

        return line;
    }

    private static int FindEndSourceLine(string[] lines)
    {
        for (var i = lines.Length - 1; i >= 0; i--)
        {
            if (lines[i].TrimStart().StartsWith("end ", StringComparison.OrdinalIgnoreCase))
                return i + 1;
        }

        return 0;
    }

    private static bool IsSteppableSourceLine(string line)
    {
        if (IsNonCodeSourceLine(line))
            return false;

        var trimmed = line.TrimStart();
        if (trimmed.StartsWith("end ", StringComparison.OrdinalIgnoreCase))
            return false;
        if (trimmed.StartsWith("endstatic", StringComparison.OrdinalIgnoreCase))
            return false;
        if (trimmed.StartsWith("program ", StringComparison.OrdinalIgnoreCase))
            return false;
        if (trimmed.StartsWith("static", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    private static bool IsNonCodeSourceLine(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Length == 0
               || trimmed.StartsWith("//", StringComparison.Ordinal)
               || trimmed.StartsWith('#');
    }

    private static bool IsLabelOnlyNasmLine(string nasmPath, int nasmLine)
    {
        if (nasmLine <= 0 || !File.Exists(nasmPath))
            return false;

        var lines = File.ReadAllLines(nasmPath);
        if (nasmLine > lines.Length)
            return false;

        return NasmLineClassifier.IsLabelOnly(lines[nasmLine - 1]);
    }

    private static int FindBeginSourceLine(SourceMapDocument? sourceMap)
    {
        if (sourceMap != null && File.Exists(sourceMap.Source))
        {
            var lines = File.ReadAllLines(sourceMap.Source);
            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].TrimStart().StartsWith("begin ", StringComparison.OrdinalIgnoreCase))
                    return i + 1;
            }
        }

        return 0;
    }

    private static IEnumerable<int> EnumerateInstructionLines(string nasmPath)
    {
        var lines = File.ReadAllText(nasmPath).Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            if (NasmLineClassifier.IsInstructionLine(lines[i]))
                yield return i + 1;
        }
    }

    private static ulong ComputeUserCodeEnd(string executablePath, IReadOnlyDictionary<ulong, int> nasmByAddress)
    {
        if (nasmByAddress.Count == 0)
            return 0;

        if (!NativeBinaryEntryPoint.TryGetEntryPoint(executablePath, out var entry))
            return 0;

        var max = entry;
        foreach (var address in nasmByAddress.Keys)
        {
            if (address > max)
                max = address;
        }

        return max + 16;
    }

    private static bool TryGetUserCodeEnd(string executablePath, string nasmPath, out ulong end)
    {
        end = 0;
        if (!NativeBinaryEntryPoint.TryGetEntryPoint(executablePath, out var entry))
            return false;

        foreach (var nasmLine in EnumerateInstructionLines(nasmPath).Reverse())
        {
            if (NasmLineAddressResolver.TryResolveUserInstruction(executablePath, nasmPath, nasmLine) is not ulong lastAddress)
                continue;

            end = lastAddress + 16;
            return true;
        }

        var maps = BuildNasmLinesByAddress(executablePath, nasmPath);
        end = ComputeUserCodeEnd(executablePath, maps);
        return end > entry;
    }
}
