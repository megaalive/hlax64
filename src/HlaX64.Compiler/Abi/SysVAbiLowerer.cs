using HlaX64.Compiler.Ir;
using HlaX64.Compiler.Options;

namespace HlaX64.Compiler.Abi;

public sealed class SysVAbiLowerer : IAbiLowerer
{
    public string Name => "linux-x64-sysv";
    public IReadOnlyList<string> ArgumentRegisters { get; } = new[] { "rdi", "rsi", "rdx", "rcx", "r8", "r9" };
    public string ReturnRegister => "rax";
    public IReadOnlyList<string> CallerSaved { get; } = new[] { "rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11" };
    public IReadOnlyList<string> CalleeSaved { get; } = new[] { "rbx", "rbp", "r12", "r13", "r14", "r15" };
    public int StackAlignment => 16;

    public LoweredFunction Lower(IrFunction function, CompilationOptions options)
    {
        var lowered = new LoweredFunction(function.Name);

        foreach (var irBlock in function.Blocks)
        {
            var loweredBlock = new LoweredBlock(irBlock.Label);

            foreach (var inst in irBlock.Instructions)
            {
                loweredBlock.Instructions.Add(LowerInstruction(inst));
            }

            lowered.Blocks.Add(loweredBlock);
        }

        return lowered;
    }

    private static LoweredInstruction LowerInstruction(IrInstruction inst)
    {
        return inst.Opcode switch
        {
            IrOpcode.LoadConstant => new LoweredInstruction($"    mov {inst.Destination}, {inst.Immediate}"),
            IrOpcode.Move => new LoweredInstruction($"    mov {string.Join(", ", inst.Operands)}"),
            IrOpcode.Add => new LoweredInstruction($"    add {string.Join(", ", inst.Operands)}"),
            IrOpcode.Subtract => new LoweredInstruction($"    sub {string.Join(", ", inst.Operands)}"),
            IrOpcode.Multiply => new LoweredInstruction($"    imul {string.Join(", ", inst.Operands)}"),
            IrOpcode.Compare => new LoweredInstruction($"    cmp {string.Join(", ", inst.Operands)}"),
            IrOpcode.Branch => new LoweredInstruction($"    jmp {inst.TargetBlock}"),
            IrOpcode.ConditionalBranch => new LoweredInstruction($"    jne {inst.TargetBlock}"),
            IrOpcode.Call => new LoweredInstruction($"    call {inst.Immediate}"),
            IrOpcode.Return => new LoweredInstruction("    ret"),
            _ => new LoweredInstruction($"    ; {inst}")
        };
    }
}
