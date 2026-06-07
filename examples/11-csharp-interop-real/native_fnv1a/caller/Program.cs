using System.Runtime.InteropServices;

var fixture = args.Length >= 1
    ? args[0]
    : Path.Combine("..", "fixtures", "sample-a.txt");

if (!File.Exists(fixture))
{
    Console.Error.WriteLine($"fixture not found: {Path.GetFullPath(fixture)}");
    Environment.Exit(1);
}

var bytes = File.ReadAllBytes(fixture);
long hash;
unsafe
{
    fixed (byte* p = bytes)
    {
        hash = NativeFnv1a.Fnv1a64((nint)p, bytes.Length);
    }
}

Console.WriteLine($"fnv1a={hash}");

internal static class NativeFnv1a
{
    [DllImport("native_fnv1a", EntryPoint = "Fnv1a64")]
    internal static extern long Fnv1a64(nint data, long length);
}
