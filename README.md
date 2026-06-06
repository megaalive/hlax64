# HlaX64 — HLA-Inspired x64 Assembly Layer for Vibe Coding

> **AI-friendly x64 assembly source layer for verified executable vibe coding.**
> Write low-level x64 with a cleaner HLA-inspired syntax. Compile to NASM,
> assemble, link, and run — all from a single `hla64` CLI.

[![Status](https://img.shields.io/badge/Fase%200%E2%80%9313-Done-green)](./docs/roadmap.md)
[![Tests](https://img.shields.io/badge/tests-118%2F118%20+%2016%2F16%20native%20+%2018%20curriculum-2ea44f)](#test-status)
[![Target](https://img.shields.io/badge/target-linux--x64--sysv%20|%20windows--x64--msabi-1f6feb)](#targets)
[![Language](https://img.shields.io/badge/language-v0.1%20Draft-blueviolet)](#language-reference)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](#license)
[![Pages](https://img.shields.io/badge/docs-GitHub%20Pages-2ea44f)](https://megaalive.github.io/hlax64/playground/index.html)

---

## Install

Pre-built archives, global tool, or clone — see **[docs/install.md](docs/install.md)**.

```bash
hla64 doctor
hla64 run examples/00-getting-started/hello.hla64
```

Tutorials: [Getting started](docs/tutorials/01-getting-started.md) · [Native routines](docs/tutorials/02-native-routines.md) · [C# interop](docs/tutorials/03-csharp-interop.md) · [MCP agents](docs/tutorials/04-mcp-agent.md)

---

## ✨ Feature Status

### Compiler Pipeline

| Area                | Status | Details |
|---------------------|--------|---------|
| **Lexer / Parser**  | ✅     | Full token set, recursive-descent parser, AST, source locations |
| **IR Pipeline**     | ✅     | AST → IR → ABI lowerer → NASM emitter (Fase 9.5 A–E) |
| **NASM Backend**    | ✅     | Emits from lowered IR; operand-order flip in ABI lowerer |
| **Semantic Analysis**| ✅    | Register & instruction validation, scope checking, diagnostics |
| **Control flow**    | ✅     | `if/else/endif`, `while/endwhile` with `=`, `<`, `>` |
| **Procedures**      | ✅     | 0–6 integer args, `@returns("rax")`, no-paren syntax |
| **Local variables** | ✅     | `var` block, stack frame with `[rbp-N]` addressing |
| **Stdlib**          | ✅     | `stdout.put("str", nl, rax, 42)` — inline syscall mode |

### Targets

| Target              | Status | Compiler flag        |
|---------------------|--------|----------------------|
| `linux-x64-sysv`    | ✅     | (default)            |
| `windows-x64-msabi` | ✅     | `--target windows-x64-msabi` |

### Runtime

| Area                | Status | Details |
|---------------------|--------|---------|
| **Linux executable**| ✅     | Freestanding `_start` entry, inline syscalls |
| **Linux shared lib**| ✅     | `--output-kind shared-library`, NASM + GCC link |
| **Windows exe**     | ✅     | `ExitProcess`, `WriteConsoleA`, LLD link |
| **Windows DLL**     | ✅     | Export procedures with `export` keyword |
| **Runtime contract**| ✅     | `HLAX64-RUNTIME-FUNCTION` markers in `src/HlaX64.Runtime/` |
| **Runtime library** | ✅     | `src/HlaX64.Runtime/` with headers + NASM sources |

### Interop

| Area                | Status | Details |
|---------------------|--------|---------|
| **C ABI export**    | ✅     | `export procedure`, `--output-kind shared-library` |
| **C header generator**| ✅   | `hla64 generate-header` emits C declarations |
| **C# P/Invoke gen** | ✅     | `hla64 generate-pinvoke` emits `[DllImport]` wrappers |

### Agent & Developer Tooling

| Area                | Status | Details |
|---------------------|--------|---------|
| **CLI**             | ✅     | `build`, `emit-nasm`, `run`, `test`, `bench`, `explain`, `explain-abi`, `format`, `generate-header`, `generate-pinvoke`, `doctor` |
| **Test runner**     | ✅     | 118 unit tests (+ 20 example compile guards) + 16 native samples + 18 curriculum manifests |
| **Benchmark runner**| ✅     | `hla64 bench` with warmup, median, binary size, JSON manifest |
| **MCP server**      | ✅     | 12 tools via stdio JSON-RPC: compile, build, run, test, explain, explain-abi, format-source, doctor, generate-header, generate-pinvoke, get-version, list-instructions |
| **ABI explainer**   | ✅     | `hla64 explain-abi --target linux-x64-sysv` (or `windows-x64-msabi`) |

---

## 🚀 Quick Start

```bash
# 1. Build everything
dotnet build

# 2. Run all tests
dotnet test

# 3. Compile a .hla64 file to NASM
dotnet run --project src/HlaX64.Cli -- emit-nasm examples/00-getting-started/hello.hla64

# 4. Build a .hla64 file into a Linux ELF executable
dotnet run --project src/HlaX64.Cli -- build examples/00-getting-started/exitcode.hla64 -o build/exitcode

# 5. Compile and run
dotnet run --project src/HlaX64.Cli -- run examples/00-getting-started/hello.hla64
```

> Toolchain: `dotnet` 10.0+ on the build machine, plus `nasm` and `gcc` (or
> WSL2 with `nasm`/`gcc` installed) for native execution. The CLI auto-detects
> WSL, MinGW-w64, or native Linux.

---

## 📚 Example Programs

### Hello world

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

### Exit code 42

```hla
program exitcode;
begin exitcode;
    mov(42, rax);
end exitcode;
```

### Procedure call (Linux SysV ABI)

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;

program main;
begin main;
    call AddTwo(10, 20);
    stdout.put("10 + 20 = ", rax, nl);
end main;
```

### Control flow + register print

```hla
program count;
begin count;
    mov(0, rcx);
    while(rcx < 5) do
        stdout.put("count = ", rcx, nl);
        add(1, rcx);
    endwhile;
end count;
```

More examples live under [`examples/`](./examples) (structured by topic).

---

## Acknowledgement

HlaX64 is inspired by the educational ideas of Randall Hyde's High Level Assembly language and *The Art of Assembly Language*. HlaX64 is an independent project and is not affiliated with or endorsed by Randall Hyde. See [docs/classic-hla-comparison.md](./docs/classic-hla-comparison.md).

---

## 🏗 Project Structure

```text
HlaX64/
├─ src/
│  ├─ HlaX64.Compiler/        # Lexer, Parser, AST, Semantic, IR, TestRunner
│  ├─ HlaX64.Backend.Nasm/    # NASM x64 code emitter
│  ├─ HlaX64.Cli/             # CLI (hla64) — build, run, test, bench, etc.
│  ├─ HlaX64.Runtime/         # Runtime library (Linux + Windows)
│  └─ HlaX64.McpServer/       # MCP server for AI agent integration
├─ tests/
│  ├─ HlaX64.Compiler.Tests/  # xUnit suite (117 tests)
│  └─ samples/                # Integration test manifests + expected output
├─ benchmarks/                # Benchmark manifests
├─ examples/                  # *.hla64 sample programs
└─ docs/                      # Language spec, ABI docs, roadmap
```

> See [`docs/compiler-architecture.md`](./docs/compiler-architecture.md) for the full pipeline diagram.

---

## 📖 Language at a Glance

- **Operand order** follows HLA: `mov(source, dest)`. The NASM backend
  reverses it to NASM's `mov dest, source` automatically.
- **Program structure** mirrors classic HLA:
  `program name; begin name; … end name;`
- **Standard library** is currently `stdout.put(...)`. More coming.
- **See [`docs/language-spec.md`](./docs/language-spec.md)** for the
  complete reference: types, registers, instructions, control flow,
  procedures, and escape sequences.

---

## 🧪 Test Status

```
$ dotnet test
Passed!  - Failed: 0, Passed: 117, Skipped: 0, Total: 117
```

| Suite               | Count | Coverage |
|---------------------|-------|----------|
| `LexerTests`        | ~14   | Keywords, registers, literals, comments, positions |
| `ParserTests`       | ~7    | Programs, calls, includes, control flow, procedures |
| `NasmEmitterTests`  | ~25   | Instruction lowering, ABI (SysV + Windows), runtime markers, hello world |
| `SemanticAnalyzerTests` | ~7 | Register & instruction validation, diagnostics |
| `TestRunnerTests`   | ~8    | Manifest loading, runner flow, source resolution |
| `WindowsAbiLowererTests` | ~11 | MS x64 arg regs, shadow space, stack alignment, calls, epilogue |
| **Total**           | **117** | All passing ✅ |

---

## 🗺 Roadmap

Phases are summarized in [`docs/roadmap.md`](./docs/roadmap.md).

| Fase | Description                          | Status |
|------|--------------------------------------|--------|
| 0    | Foundation & repo setup              | ✅      |
| 1    | Lexer & Parser MVP                   | ✅      |
| 2    | NASM Backend MVP                     | ✅      |
| 3    | Toolchain build (Linux x64)          | ✅      |
| 4    | Runtime: `stdout.put`                | ✅      |
| 5    | Semantic Analyzer                    | ✅      |
| 6    | Procedure & SysV ABI                 | ✅      |
| 7    | Control flow                         | ✅      |
| 8    | Local variables & stack frame        | ✅      |
| 9    | Test runner CLI                      | ✅      |
| **9.5** | **Compiler Architecture Stabilization (IR, ABI lowerer, type system, runtime contract, native tests)** | ✅ |
| 10   | Benchmark runner                     | ✅      |
| 11   | Windows x64 backend                  | ✅      |
| 12   | C ABI & C# interop generator         | ✅      |
| 13   | MCP server (for AI agents)           | ✅      |
| 14   | LSP & editor tooling                 | 🚧 (diagnostics LSP MVP) |
| 15   | AI Assembly Lab / IDE plugin         | ⏳      |

> **Aturan**: jangan sentuh Fase 14–15 sebelum Definition of Done Fase 9.5 terpenuhi. Lihat [`docs/roadmap.md`](./docs/roadmap.md).

### 📚 Dokumentasi tambahan

- [`docs/roadmap.md`](./docs/roadmap.md) — peta fase + Tier eksekusi aktif.
- [`docs/compiler-architecture.md`](./docs/compiler-architecture.md) — diagram pipeline IR + 7 workstream.
- [`docs/runtime-contract.md`](./docs/runtime-contract.md) — format clobber metadata + kontrak SysV/Windows.
- [`docs/runtime-matrix.md`](./docs/runtime-matrix.md) — target × output kind defaults.
- [`docs/compatibility.md`](./docs/compatibility.md) — breaking change policy.
- [`docs/diagnostics.md`](./docs/diagnostics.md) — diagnostic code catalog.
- [`docs/examples.md`](./docs/examples.md) — katalog program contoh & cara menjalankan.
- [`docs/install.md`](./docs/install.md) — unduh release, global tool, uninstall.
- [`docs/architecture.md`](./docs/architecture.md) — pipeline diagram & project map.
- [`docs/tutorials/`](./docs/tutorials/) — tutorial series (beginner → MCP).
- [`docs/classic-hla-comparison.md`](./docs/classic-hla-comparison.md) — perbandingan dengan HLA klasik.

---

## 🤖 AI-Friendly Design

HlaX64 is built **for** AI coding agents:

- **Explicit, consistent syntax** — no alternative forms to guess between.
- **Operand order is HLA-style** (`source, dest`) which is more natural
  to read.
- **Diagnostics carry line + column** and include "Did you mean …?"
  suggestions (via the semantic analyzer).
- **Generated NASM is annotated** with `; RUNTIME: <fn>` comments at
  every runtime call site so agents and humans can trace what the
  compiler emitted.
- **Runtime is split** between inline MVP syscalls and a stable library
  so Fase 6+ can switch to calls without changing user source.

See [`docs/language-spec.md`](./docs/language-spec.md) for full details.

---

## 🤝 Community

- [Contributing Guide](CONTRIBUTING.md) — how to build, test, and submit changes
- [Install Guide](docs/install.md) — release archives and global tool
- [Development Guide](docs/development.md) — local setup and CI parity
- [Governance](GOVERNANCE.md) — decision process and labels
- [Code of Conduct](CODE_OF_CONDUCT.md) — our community standards
- [Security Policy](SECURITY.md) — how to report vulnerabilities
- [Support](SUPPORT.md) — documentation and getting help
- [MCP Tools](docs/mcp-tools.md) — agent tool catalog
- [Changelog](CHANGELOG.md) — release history

## 📄 License

MIT
