using HlaX64.Compiler;
using HlaX64.Cli.Commands;
using Spectre.Console.Cli;

var app = new CommandApp();
app.Configure(config =>
{
    config.PropagateExceptions();
    config.SetApplicationName("hla64");
    config.SetApplicationVersion(Compilation.GetVersion());

    config.AddCommand<BuildCommand>("build")
        .WithDescription("Build a .hla64 source file into an executable");

    config.AddCommand<EmitNasmCommand>("emit-nasm")
        .WithDescription("Emit NASM assembly output from a .hla64 source file");

    config.AddCommand<RunCommand>("run")
        .WithDescription("Build and run a .hla64 source file");

    config.AddCommand<TestCommand>("test")
        .WithDescription("Run all test manifests in a directory");

    config.AddCommand<ExplainAbiCommand>("explain-abi")
        .WithDescription("Print ABI details for a target triple (e.g. linux-x64-sysv, windows-x64-msabi)");

    config.AddCommand<ExplainCommand>("explain")
        .WithDescription("Show IR, ABI lowering, and NASM for a .hla64 source file");

    config.AddCommand<FormatCommand>("format")
        .WithDescription("Format .hla64 source files (optional --check)");

    config.AddCommand<BenchCommand>("bench")
        .WithDescription("Benchmark .hla64 programs (warmup + measured iterations)");

    config.AddCommand<VerifyStackCommand>("verify-stack")
        .WithDescription("Verify stack frame layout and prologue/epilogue for a .hla64 file");

    config.AddCommand<VerifyAbiCommand>("verify-abi")
        .WithDescription("Report ABI parameter mapping and extern symbols for a .hla64 file");

    config.AddCommand<GenerateHeaderCommand>("generate-header")
        .WithDescription("Generate a C header file with declarations for exported procedures");

    config.AddCommand<GeneratePInvokeCommand>("generate-pinvoke")
        .WithDescription("Generate C# P/Invoke declarations for exported procedures");

    config.AddCommand<DoctorCommand>("doctor")
        .WithDescription("Check development toolchain and environment");
});

return app.Run(args);