using System.Runtime.InteropServices;

var fixture = args.Length >= 1
    ? args[0]
    : Path.Combine("..", "fixtures", "sample-b.txt");

if (!File.Exists(fixture))
{
    Console.Error.WriteLine($"fixture not found: {Path.GetFullPath(fixture)}");
    Environment.Exit(1);
}

var bytes = File.ReadAllBytes(fixture);
long lines;
unsafe
{
    fixed (byte* p = bytes)
    {
        lines = NativeCountLines.CountLines((nint)p, bytes.Length);
    }
}

Console.WriteLine($"lines: {lines}");

internal static class NativeCountLines
{
    [DllImport("native_count_lines", EntryPoint = "CountLines")]
    internal static extern long CountLines(nint data, long length);
}
