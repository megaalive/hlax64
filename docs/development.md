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

## Cursor MCP (project-local only)

MCP servers for this repo are configured in [`.cursor/mcp.json`](../.cursor/mcp.json) — **not** in global Cursor settings.

| Server | Purpose |
|--------|---------|
| `codegraph` | CodeGraph Codex — symbol search, impact, build/test diagnostics |
| `agentmemory` | AgentMemory profile **HlaX64** @ `http://127.0.0.1:5123` |

Prerequisites: AgentMemory API running on port 5123; CodeGraph index at `.codegraph/codegraph.json` (run `scripts/reindex-codegraph.ps1` after code changes, or set `CODEGRAPH_AUTO_INDEX=1` before `dotnet build`).

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
dotnet test   # 314 unit tests; native samples require Linux toolchain in CI
```

### Assembly Lab (Phase 15)

Cross-platform Avalonia desktop app for visual pipeline exploration:

```bash
dotnet run --project src/HlaX64.AssemblyLab
```

Features include pipeline tabs (IR/NASM/ABI), build/run/proof bundle, **Settings** for toolchain paths, and an **embedded PTY terminal** (`Controls/LabTerminalControl`, `Services/InteractivePtySession`, `Porta.Pty` + `AvaloniaTerminal`). The terminal uses an overlay caret (`TerminalTypingCaret`) because Windows `cmd.exe` often omits ANSI cursor positioning after commands like `dir`.

Publish a release bundle:

```powershell
.\scripts\publish-assembly-lab.ps1 -Rids win-x64,linux-x64
.\scripts\smoke-assembly-lab-bundle.ps1 -BundleDir publish\assembly-lab\win-x64
```

See [docs/tutorials/06-assembly-lab.md](tutorials/06-assembly-lab.md) and [RFC 0024](../rfcs/0024-assembly-lab.md).

### Language Server (Phase 14 MVP)

```bash
dotnet run --project src/HlaX64.LanguageServer
```

Capabilities: **publishDiagnostics** (incl. bounds + verification warnings HLAX0060–63), **hover**, **completion**, **definition**, **documentSymbol**, **documentFormatting**.

**VS Code / Cursor:** install the extension from `editors/vscode/` (run `npm install` once), then **Developer: Install Extension from Location**. The extension starts the language server via `dotnet run` automatically. Format-on-save is enabled for `.hla64` by default.

Optional CLI warnings on `build`, `emit-nasm`, `run`, `explain`:

| Flag | Diagnostics |
|------|-------------|
| `-Wbounds` | HLAX0030 array index bounds |
| `-Wdefinite` | HLAX0060 use before assignment |
| `-Wunreachable` | HLAX0061/62 unreachable / missing return |
| `-Wliveness` | HLAX0063 live register across call |
| `-Wverify` | all Phase 18 verification warnings |

Verification CLI (Phase 18):

```bash
dotnet run --project src/HlaX64.Cli -- verify-stack examples/curriculum/01-arithmetic/add-two.hla64
dotnet run --project src/HlaX64.Cli -- verify-abi examples/curriculum/06-abi/stack-args-sysv.hla64 --json
```

### Fuzz / robustness tests (Phase 18)

`ParserRobustnessTests` and `FuzzTests` exercise random ASCII/UTF-8 input, formatter round-trip on valid fixtures, and manifest JSON parsing. See [RFC 0014](../rfcs/0014-verification-tooling.md).

See [diagnostics.md](diagnostics.md) for HLAX0030 and HLAX0060–68.

See [editors/vscode/README.md](../editors/vscode/README.md).

### Static playground

Open [docs/playground/index.html](../docs/playground/index.html) (GitHub Pages compatible).

On Windows, use `.\scripts\build.ps1` for a scripted restore + build.

## Running the CLI without installing

```bash
dotnet run --project src/HlaX64.Cli -- --version
dotnet run --project src/HlaX64.Cli -- run examples/curriculum/00-getting-started/hello.hla64
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
src/HlaX64.AssemblyLab/  Avalonia Assembly Lab (Phase 15)
src/HlaX64.McpServer/    MCP JSON-RPC server
tests/HlaX64.Compiler.Tests/  xUnit
tests/HlaX64.AssemblyLab.Tests/  Assembly Lab backend tests
tests/samples/           Native integration manifests
examples/                User-facing sample programs (29 curriculum manifests)
editors/vscode/          VS Code grammar, snippets, LSP client
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

Follow [CONTRIBUTING.md](rules/CONTRIBUTING.md):

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
