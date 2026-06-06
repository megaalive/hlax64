using System.ComponentModel;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

/// <summary>
/// Prints ABI details for a given target triple.
/// Currently supports linux-x64-sysv and windows-x64-msabi (planned).
/// This is a documentation/exploration tool, not part of the build pipeline.
/// </summary>
public sealed class ExplainAbiCommand : Command<ExplainAbiCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Target triple (e.g. linux-x64-sysv, windows-x64-msabi)")]
        [CommandOption("-t|--target")]
        [DefaultValue("linux-x64-sysv")]
        public string Target { get; set; } = "linux-x64-sysv";
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        return settings.Target.ToLowerInvariant() switch
        {
            "linux-x64-sysv"   => PrintLinuxSysV(),
            "windows-x64-msabi" => PrintWindowsMsAbi(),
            _ => PrintUnknown(settings.Target)
        };
    }

    private static int PrintLinuxSysV()
    {
        Console.WriteLine("ABI: linux-x64-sysv (Linux x64 System V ABI, AMD64)");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
        Console.WriteLine("Argument registers (left-to-right):");
        Console.WriteLine("  arg1 -> rdi");
        Console.WriteLine("  arg2 -> rsi");
        Console.WriteLine("  arg3 -> rdx");
        Console.WriteLine("  arg4 -> rcx");
        Console.WriteLine("  arg5 -> r8");
        Console.WriteLine("  arg6 -> r9");
        Console.WriteLine("  arg7+ -> stack (caller-allocated, right-to-left push)");
        Console.WriteLine();
        Console.WriteLine("Return value:");
        Console.WriteLine("  64-bit int/ptr -> rax");
        Console.WriteLine("  struct <= 16 bytes -> rax + rdx");
        Console.WriteLine("  larger struct -> caller-allocated memory, rax = pointer");
        Console.WriteLine();
        Console.WriteLine("Caller-saved (may be clobbered by callee):");
        Console.WriteLine("  rax, rcx, rdx, rsi, rdi, r8, r9, r10, r11");
        Console.WriteLine();
        Console.WriteLine("Callee-saved (preserved across call):");
        Console.WriteLine("  rbx, rbp, r12, r13, r14, r15");
        Console.WriteLine();
        Console.WriteLine("Scratch / temporaries: rax, rcx, rdx, rsi, rdi, r8..r11");
        Console.WriteLine();
        Console.WriteLine("Stack alignment: RSP must be 0 (mod 16) just BEFORE `call`.");
        Console.WriteLine("  i.e. just after the call instruction executes, RSP is 8 (mod 16).");
        Console.WriteLine();
        Console.WriteLine("Red zone: 128 bytes below RSP, leaf functions may use without adjusting.");
        Console.WriteLine("  HlaX64 v0.1 does NOT rely on the red zone (for portability).");
        Console.WriteLine();
        Console.WriteLine("Float / SSE args (planned, not MVP):");
        Console.WriteLine("  XMM0..XMM7 for float/double, XMM0+XMM1 for struct of 2.");
        Console.WriteLine();
        Console.WriteLine("Reference:");
        Console.WriteLine("  System V ABI x86-64: https://refspecs.linuxfoundation.org/elf/x86_64-abi-0.99.pdf");
        return 0;
    }

    private static int PrintWindowsMsAbi()
    {
        Console.WriteLine("ABI: windows-x64-msabi (Microsoft x64, x64 calling convention)");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
        Console.WriteLine("STATUS: implemented (Fase 11).");
        Console.WriteLine();
        Console.WriteLine("Argument registers:");
        Console.WriteLine("  arg1 -> rcx");
        Console.WriteLine("  arg2 -> rdx");
        Console.WriteLine("  arg3 -> r8");
        Console.WriteLine("  arg4 -> r9");
        Console.WriteLine("  arg5+ -> stack (caller-allocated, right-to-left push)");
        Console.WriteLine();
        Console.WriteLine("Return value: rax (scalar <= 64 bit), XMM0 (float/double).");
        Console.WriteLine();
        Console.WriteLine("Caller-saved: rax, rcx, rdx, r8, r9, r10, r11");
        Console.WriteLine("Callee-saved: rbx, rbp, rdi, rsi, r12, r13, r14, r15");
        Console.WriteLine();
        Console.WriteLine("Shadow space: 32 bytes (4 quadwords) reserved by the caller just");
        Console.WriteLine("  above the return address. The callee may use it freely but must");
        Console.WriteLine("  preserve it across nested calls.");
        Console.WriteLine();
        Console.WriteLine("Stack alignment: RSP must be 8 (mod 16) just BEFORE `call`.");
        Console.WriteLine("  i.e. just after the call instruction executes, RSP is 0 (mod 16).");
        Console.WriteLine();
        Console.WriteLine("Float / SSE args (planned): XMM0..XMM3.");
        Console.WriteLine();
        Console.WriteLine("Reference:");
        Console.WriteLine("  Microsoft x64 ABI: https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention");
        return 0;
    }

    private static int PrintUnknown(string target)
    {
        Console.Error.WriteLine($"Unknown target '{target}'.");
        Console.Error.WriteLine("Supported targets:");
        Console.Error.WriteLine("  linux-x64-sysv     (implemented, MVP)");
        Console.Error.WriteLine("  windows-x64-msabi  (implemented, Fase 11)");
        return 1;
    }
}
