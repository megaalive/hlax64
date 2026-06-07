namespace HlaX64.DebugAdapter;

/// <summary>Runs debug sessions against a copy so the build output stays linkable.</summary>
public static class DebugShadowExecutable
{
    public static string? TryCreate(string sourceExecutable)
    {
        if (!OperatingSystem.IsWindows() || !File.Exists(sourceExecutable))
            return null;

        var directory = Path.GetDirectoryName(sourceExecutable);
        if (string.IsNullOrEmpty(directory))
            return null;

        var baseName = Path.GetFileNameWithoutExtension(sourceExecutable);
        var shadowPath = Path.Combine(directory, $"{baseName}-labdbg.exe");

        try
        {
            File.Copy(sourceExecutable, shadowPath, overwrite: true);
            return shadowPath;
        }
        catch
        {
            return null;
        }
    }

    public static void TryDelete(string? shadowPath)
    {
        if (string.IsNullOrWhiteSpace(shadowPath))
            return;

        try { File.Delete(shadowPath); } catch { /* shadow may still be mapped */ }
    }
}
