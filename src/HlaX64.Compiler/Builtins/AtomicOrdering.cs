namespace HlaX64.Compiler.Builtins;

public enum AtomicOrdering
{
    Relaxed,
    Acquire,
    Release,
    AcqRel,
    SeqCst
}

public static class AtomicOrderingParser
{
    public static bool TryParse(string name, out AtomicOrdering ordering)
    {
        ordering = name.ToLowerInvariant() switch
        {
            "relaxed" => AtomicOrdering.Relaxed,
            "acquire" => AtomicOrdering.Acquire,
            "release" => AtomicOrdering.Release,
            "acq_rel" => AtomicOrdering.AcqRel,
            "seq_cst" => AtomicOrdering.SeqCst,
            _ => default
        };
        return name.ToLowerInvariant() is "relaxed" or "acquire" or "release" or "acq_rel" or "seq_cst";
    }

    public static string EmitLoadFence(AtomicOrdering ordering)
        => ordering switch
        {
            AtomicOrdering.Acquire or AtomicOrdering.AcqRel or AtomicOrdering.SeqCst => "    lfence",
            _ => ""
        };

    public static string EmitStoreFence(AtomicOrdering ordering)
        => ordering switch
        {
            AtomicOrdering.Release or AtomicOrdering.AcqRel or AtomicOrdering.SeqCst => "    sfence",
            _ => ""
        };

    public static string EmitCompilerBarrier()
        => "    ; compiler barrier\n    mfence    ; full fence (MVP)";
}
