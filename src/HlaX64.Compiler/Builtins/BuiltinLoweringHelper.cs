using System.Text;
using HlaX64.Compiler.Ir;

namespace HlaX64.Compiler.Builtins;

public static class BuiltinLoweringHelper
{
    public static bool TryLowerCall(string name, IrInstruction inst, Func<IrValue?, string> resolve,
        out string asm)
    {
        asm = "";
        if (BuiltinNames.IsSimd(name))
            return TryLowerSimd(name, inst, resolve, out asm);
        if (BuiltinNames.IsAtomic(name))
            return TryLowerAtomic(name, inst, resolve, out asm);
        return false;
    }

    private static bool TryLowerSimd(string name, IrInstruction inst, Func<IrValue?, string> resolve,
        out string asm)
    {
        var sb = new StringBuilder();
        switch (name.ToLowerInvariant())
        {
            case "simd.add_f64x4":
                if (inst.Operands.Count < 2) { asm = ""; return false; }
                var dst = resolve(inst.Operands[0]);
                var src = resolve(inst.Operands[1]);
                sb.AppendLine($"    vaddpd {dst}, {src}");
                asm = sb.ToString().TrimEnd();
                return true;

            case "simd.load_f64x4":
                if (inst.Operands.Count < 1) { asm = ""; return false; }
                var ptr = resolve(inst.Operands[0]);
                var dest = inst.Operands.Count >= 2 ? resolve(inst.Operands[1]) : "ymm0";
                if (ptr.StartsWith('['))
                    sb.AppendLine($"    vmovapd {dest}, {ptr}");
                else
                    sb.AppendLine($"    vmovapd {dest}, [{ptr}]");
                asm = sb.ToString().TrimEnd();
                return true;

            case "simd.store_f64x4":
                if (inst.Operands.Count < 2) { asm = ""; return false; }
                var storePtr = resolve(inst.Operands[0]);
                var val = resolve(inst.Operands[1]);
                if (storePtr.StartsWith('['))
                    sb.AppendLine($"    vmovapd {storePtr}, {val}");
                else
                    sb.AppendLine($"    vmovapd [{storePtr}], {val}");
                asm = sb.ToString().TrimEnd();
                return true;
        }

        asm = "";
        return false;
    }

    private static bool TryLowerAtomic(string name, IrInstruction inst, Func<IrValue?, string> resolve,
        out string asm)
    {
        var sb = new StringBuilder();
        switch (name.ToLowerInvariant())
        {
            case "atomic.load":
                if (inst.Operands.Count < 2) { asm = ""; return false; }
                var loadPtr = resolve(inst.Operands[0]);
                var loadOrder = GetOrderingOperand(inst, 1);
                var loadMem = loadPtr.StartsWith('[') ? loadPtr : $"[{loadPtr}]";
                sb.AppendLine(AtomicOrderingParser.EmitLoadFence(loadOrder));
                sb.AppendLine($"    mov rax, {loadMem}");
                sb.AppendLine(AtomicOrderingParser.EmitCompilerBarrier());
                asm = sb.ToString().TrimEnd();
                return true;

            case "atomic.store":
                if (inst.Operands.Count < 3) { asm = ""; return false; }
                var storePtr = resolve(inst.Operands[0]);
                var storeVal = resolve(inst.Operands[1]);
                var storeOrder = GetOrderingOperand(inst, 2);
                var storeMem = storePtr.StartsWith('[') ? storePtr : $"[{storePtr}]";
                sb.AppendLine(AtomicOrderingParser.EmitCompilerBarrier());
                sb.AppendLine($"    mov {storeMem}, {storeVal}");
                sb.AppendLine(AtomicOrderingParser.EmitStoreFence(storeOrder));
                asm = sb.ToString().TrimEnd();
                return true;

            case "atomic.fetch_add":
                if (inst.Operands.Count < 3) { asm = ""; return false; }
                var faPtr = resolve(inst.Operands[0]);
                var delta = resolve(inst.Operands[1]);
                var faOrder = GetOrderingOperand(inst, 2);
                var faMem = faPtr.StartsWith('[') ? faPtr : $"[{faPtr}]";
                sb.AppendLine(AtomicOrderingParser.EmitCompilerBarrier());
                if (!delta.Equals("rax", StringComparison.OrdinalIgnoreCase))
                    sb.AppendLine($"    mov rax, {delta}");
                sb.AppendLine($"    lock xadd {faMem}, rax");
                sb.AppendLine(AtomicOrderingParser.EmitLoadFence(faOrder));
                asm = sb.ToString().TrimEnd();
                return true;
        }

        asm = "";
        return false;
    }

    private static AtomicOrdering GetOrderingOperand(IrInstruction inst, int index)
    {
        if (index >= inst.Operands.Count) return AtomicOrdering.SeqCst;
        var op = inst.Operands[index];
        if (op.Name?.StartsWith("order:", StringComparison.Ordinal) == true &&
            AtomicOrderingParser.TryParse(op.Name[6..], out var ord))
            return ord;
        return AtomicOrdering.SeqCst;
    }

    public static bool IsAvx2Mnemonic(string mnemonic)
        => BuiltinNames.Avx2Mnemonics.Contains(mnemonic);

    public static string FormatAvx2Instruction(string mnemonic, string dst, string src)
    {
        mnemonic = mnemonic.ToLowerInvariant();
        return mnemonic switch
        {
            "vxorpd" => $"    {mnemonic} {dst}, {dst}, {src}",
            _ => $"    {mnemonic} {dst}, {src}"
        };
    }
}
