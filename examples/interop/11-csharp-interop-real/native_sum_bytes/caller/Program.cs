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
long sum;
unsafe
{
    fixed (byte* p = bytes)
    {
        sum = NativeSumBytes.SumBytes((nint)p, bytes.Length);
    }
}

Console.WriteLine($"sum={sum}");

internal static class NativeSumBytes
{
    [DllImport("native_sum_bytes", EntryPoint = "SumBytes")]
    internal static extern long SumBytes(nint data, long length);
}
