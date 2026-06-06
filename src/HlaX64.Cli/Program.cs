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
});

return app.Run(args);