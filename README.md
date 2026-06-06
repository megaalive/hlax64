# HlaX64 — HLA-Inspired x64 Assembly Layer for Executable Vibe Coding

**HlaX64** is an AI-friendly x64 assembly language and toolchain that compiles to NASM output. It is designed for AI coding agents and humans who want to write low-level x64 code with a cleaner, more expressive syntax — inspired by the original HLA (High Level Assembly) language.

## Status

🚧 **Phase 0 — Foundation** (in progress)

## Project Structure

```
HlaX64/
├─ src/
│  ├─ HlaX64.Compiler/        # Lexer, Parser, AST, Semantic Analysis
│  ├─ HlaX64.Backend.Nasm/    # NASM x64 code emitter
│  ├─ HlaX64.Cli/             # Command-line interface (hla64)
│  └─ HlaX64.Runtime/         # Runtime library (syscalls, stdlib)
├─ tests/
│  ├─ HlaX64.Compiler.Tests/  # Compiler unit tests
│  └─ samples/                # Integration test samples
├─ docs/                      # Documentation
├─ examples/                  # Example .hla64 programs
└─ scripts/                   # Build/test scripts
```

## Quick Start

```bash
# Build the project
dotnet build

# Run tests
dotnet test

# Compile a .hla64 file
dotnet run --project src/HlaX64.Cli -- build examples/hello.hla64

# Emit NASM output only
dotnet run --project src/HlaX64.Cli -- emit-nasm examples/hello.hla64
```

## Example

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

## Target ABIs

| Target              | Status       |
|---------------------|--------------|
| `linux-x64-sysv`    | Planned      |
| `windows-x64-msabi` | Future       |

## License

MIT