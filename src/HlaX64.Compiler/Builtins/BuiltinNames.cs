namespace HlaX64.Compiler.Builtins;

public static class BuiltinNames
{
    public const string SimdPrefix = "simd.";
    public const string AtomicPrefix = "atomic.";

    public static readonly HashSet<string> SimdIntrinsics = new(StringComparer.OrdinalIgnoreCase)
    {
        "simd.add_f64x4",
        "simd.load_f64x4",
        "simd.store_f64x4"
    };

    public static readonly HashSet<string> AtomicIntrinsics = new(StringComparer.OrdinalIgnoreCase)
    {
        "atomic.load",
        "atomic.store",
        "atomic.fetch_add"
    };

    public static readonly HashSet<string> AtomicOrderings = new(StringComparer.OrdinalIgnoreCase)
    {
        "relaxed", "acquire", "release", "acq_rel", "seq_cst"
    };

    public static readonly HashSet<string> Avx2Mnemonics = new(StringComparer.OrdinalIgnoreCase)
    {
        "vaddpd", "vmovapd", "vxorpd"
    };

    public static bool IsSimd(string name) => SimdIntrinsics.Contains(name);
    public static bool IsAtomic(string name) => AtomicIntrinsics.Contains(name);
    public static bool IsBuiltin(string name) => IsSimd(name) || IsAtomic(name);
}
