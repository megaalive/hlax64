namespace HlaX64.DebugAdapter;

public static class DebugStopClassifier
{
    public static bool IsProgramEnded(DebugStopInfo? info)
    {
        if (info == null)
            return false;

        if (info.Reason is "exited" or "exited-normally" or "exited-signalled")
            return true;

        var frame = info.Frames.FirstOrDefault()?.Name ?? "";
        if (string.IsNullOrWhiteSpace(frame))
            return false;

        return frame.Contains("KernelBase", StringComparison.OrdinalIgnoreCase)
               || frame.Contains("ntdll", StringComparison.OrdinalIgnoreCase)
               || frame.Contains("TestCreate", StringComparison.OrdinalIgnoreCase);
    }
}
