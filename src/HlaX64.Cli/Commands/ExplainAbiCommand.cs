using System.ComponentModel;
using HlaX64.Cli.Json;
using HlaX64.Compiler;
using Spectre.Console.Cli;

namespace HlaX64.Cli.Commands;

/// <summary>
/// Prints ABI details for a given target triple.
/// </summary>
public sealed class ExplainAbiCommand : Command<ExplainAbiCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [Description("Target triple (e.g. linux-x64-sysv, windows-x64-msabi)")]
        [CommandOption("-t|--target")]
        [DefaultValue("linux-x64-sysv")]
        public string Target { get; set; } = "linux-x64-sysv";

        [Description("Output ABI details as JSON")]
        [CommandOption("--json")]
        public bool Json { get; set; }
    }

    protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellation)
    {
        var info = GetAbiInfo(settings.Target);
        if (info == null)
        {
            if (settings.Json)
            {
                CliJson.Write(new
                {
                    schemaVersion = CliJson.SchemaVersion,
                    success = false,
                    version = Compilation.GetVersion(),
                    target = settings.Target,
                    error = $"Unknown target '{settings.Target}'."
                });
            }
            else
            {
                PrintUnknown(settings.Target);
            }
            return 1;
        }

        if (settings.Json)
        {
            CliJson.Write(new
            {
                schemaVersion = CliJson.SchemaVersion,
                success = true,
                version = Compilation.GetVersion(),
                abi = info
            });
            return 0;
        }

        PrintHumanReadable(info);
        return 0;
    }

    private static AbiInfo? GetAbiInfo(string target) => target.ToLowerInvariant() switch
    {
        "linux-x64-sysv" => new AbiInfo
        {
            Target = "linux-x64-sysv",
            Name = "Linux x64 System V ABI (AMD64)",
            Status = "implemented",
            ArgumentRegisters = ["rdi", "rsi", "rdx", "rcx", "r8", "r9"],
            ReturnRegister = "rax",
            CallerSaved = ["rax", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11"],
            CalleeSaved = ["rbx", "rbp", "r12", "r13", "r14", "r15"],
            StackAlignment = "RSP mod 16 == 0 before call",
            ShadowSpaceBytes = 0,
            Reference = "https://refspecs.linuxfoundation.org/elf/x86_64-abi-0.99.pdf"
        },
        "windows-x64-msabi" => new AbiInfo
        {
            Target = "windows-x64-msabi",
            Name = "Microsoft x64 calling convention",
            Status = "implemented",
            ArgumentRegisters = ["rcx", "rdx", "r8", "r9"],
            ReturnRegister = "rax",
            CallerSaved = ["rax", "rcx", "rdx", "r8", "r9", "r10", "r11"],
            CalleeSaved = ["rbx", "rbp", "rdi", "rsi", "r12", "r13", "r14", "r15"],
            StackAlignment = "RSP mod 16 == 8 before call",
            ShadowSpaceBytes = 32,
            Reference = "https://learn.microsoft.com/en-us/cpp/build/x64-calling-convention"
        },
        _ => null
    };

    private static void PrintHumanReadable(AbiInfo info)
    {
        Console.WriteLine($"ABI: {info.Target} ({info.Name})");
        Console.WriteLine(new string('-', 60));
        Console.WriteLine();
        Console.WriteLine($"STATUS: {info.Status}.");
        Console.WriteLine();
        Console.WriteLine("Argument registers (left-to-right):");
        for (int i = 0; i < info.ArgumentRegisters.Count; i++)
            Console.WriteLine($"  arg{i + 1} -> {info.ArgumentRegisters[i]}");
        Console.WriteLine();
        Console.WriteLine($"Return value: {info.ReturnRegister}");
        Console.WriteLine();
        Console.WriteLine("Caller-saved: " + string.Join(", ", info.CallerSaved));
        Console.WriteLine("Callee-saved: " + string.Join(", ", info.CalleeSaved));
        Console.WriteLine();
        Console.WriteLine($"Stack alignment: {info.StackAlignment}");
        if (info.ShadowSpaceBytes > 0)
            Console.WriteLine($"Shadow space: {info.ShadowSpaceBytes} bytes");
        Console.WriteLine();
        Console.WriteLine("Reference: " + info.Reference);
    }

    private static int PrintUnknown(string target)
    {
        Console.Error.WriteLine($"Unknown target '{target}'.");
        Console.Error.WriteLine("Supported targets:");
        Console.Error.WriteLine("  linux-x64-sysv");
        Console.Error.WriteLine("  windows-x64-msabi");
        return 1;
    }

    private sealed class AbiInfo
    {
        public string Target { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "";
        public List<string> ArgumentRegisters { get; set; } = [];
        public string ReturnRegister { get; set; } = "";
        public List<string> CallerSaved { get; set; } = [];
        public List<string> CalleeSaved { get; set; } = [];
        public string StackAlignment { get; set; } = "";
        public int ShadowSpaceBytes { get; set; }
        public string Reference { get; set; } = "";
    }
}
