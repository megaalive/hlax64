# Development Guide

Setup, build, test, and debug HlaX64 locally.

## Prerequisites

| Tool | Purpose |
|------|---------|
| [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) | Build CLI, compiler, tests, MCP server |
| [NASM](https://nasm.us) | Assemble emitted `.nasm` to object files |
| **Linux target** | `gcc` or `ld` (native or via WSL on Windows) |
| **Windows target** | `lld-link` (LLVM) or MSVC `link.exe` |

Run `hla64 doctor` (or `dotnet run --project src/HlaX64.Cli -- doctor`) to verify your environment.

On Windows, add NASM to PATH after install:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/setup-toolchain-path.ps1
```

Linux ELF `hla64 run` on Windows needs WSL Ubuntu with `gcc` (MinGW cannot link elf64 objects).

### Install as global tool

See [install.md](install.md) for release archives and uninstall steps.

```bash
dotnet pack src/HlaX64.Cli/HlaX64.Cli.csproj -c Release
dotnet tool install --global --add-source ./src/HlaX64.Cli/bin/Release HlaX64.Cli
hla64 --version
```

Or run from source without installing (below).

## Clone and build

```bash
git clone https://github.com/megaalive/hlax64.git
cd hlax64
dotnet build
dotnet test   # 150+ unit tests; native samples require Linux toolchain in CI
```

### Language Server (Phase 14 MVP)

```bash
dotnet run --project src/HlaX64.LanguageServer
```

Capabilities: **publishDiagnostics**, **hover** (mnemonics, keywords, types, registers), **completion**.

**VS Code / Cursor:** install the extension from `editors/vscode/` (run `npm install` once), then **Developer: Install Extension from Location**. The extension starts the language server via `dotnet run` automatically.

See [editors/vscode/README.md](../editors/vscode/README.md).

### Static playground

Open [docs/playground/index.html](../docs/playground/index.html) (GitHub Pages compatible).

On Windows, use `.\scripts\build.ps1` for a scripted restore + build.

## Running the CLI without installing

```bash
dotnet run --project src/HlaX64.Cli -- --version
dotnet run --project src/HlaX64.Cli -- run examples/00-getting-started/hello.hla64
dotnet run --project src/HlaX64.Cli -- test tests/samples
```

## Native integration tests

Sample programs under `tests/samples/` each have a `manifest.json` with expected stdout and exit code.

Curriculum examples use `tests/examples-curriculum/` (paths into `examples/`).

```bash
dotnet run --project src/HlaX64.Cli -- test tests/samples
dotnet run --project src/HlaX64.Cli -- test tests/examples-curriculum
dotnet run --project src/HlaX64.Cli -- test tests/samples --filter hello
dotnet run --project src/HlaX64.Cli -- test tests/samples --json
```

Pipeline: **source → compiler → NASM → linker → binary → run → assert**.

Use `--compile-only` when NASM or a linker is unavailable (compile step only).

## Project layout

```text
src/HlaX64.Compiler/     Lexer, parser, AST, semantic, IR, ABI lowerers, test runner
src/HlaX64.Backend.Nasm/ NASM emitter
src/HlaX64.Runtime/      Platform runtime (Linux + Windows NASM)
src/HlaX64.Cli/          hla64 commands
src/HlaX64.McpServer/    MCP JSON-RPC server
tests/HlaX64.Compiler.Tests/  xUnit
tests/samples/           Native integration manifests
examples/                User-facing sample programs (16 curriculum examples)
editors/vscode/          VS Code grammar + snippets
rfcs/                    Design RFCs
tests/conformance/       Valid/invalid language conformance cases
docs/                    Specifications and guides
schemas/                 JSON Schema for CLI machine output
```

See [compiler-architecture.md](compiler-architecture.md) for the full pipeline.

## Debugging tips

| Task | Command |
|------|---------|
| Emit NASM only | `hla64 emit-nasm file.hla64` |
| Inspect lowering | `hla64 explain file.hla64` |
| Check formatting | `hla64 format examples --check` |
| Inspect ABI | `hla64 explain-abi --target linux-x64-sysv` |
| Machine-readable test results | `hla64 test tests/samples --json` |
| Benchmark | `hla64 bench benchmarks/count.json` |

## Making changes

Follow [CONTRIBUTING.md](../CONTRIBUTING.md):

- Language changes → update lexer through backend + spec + samples
- Diagnostics → [diagnostics.md](diagnostics.md)
- Breaking changes → [compatibility.md](compatibility.md) + CHANGELOG

## CI parity

GitHub Actions runs on `ubuntu-latest` and `windows-latest`:

- `dotnet build` / `dotnet test`
- `hla64 doctor`
- Linux: full native sample test suite
- Windows: Windows MS ABI build smoke test

See [.github/workflows/ci.yml](../.github/workflows/ci.yml).
