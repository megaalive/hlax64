# HlaX64 — HLA-Inspired x64 Assembly Layer for Vibe Coding

> **AI-friendly x64 assembly source layer for verified executable vibe coding.**
> Write low-level x64 with a cleaner HLA-inspired syntax. Compile to NASM,
> assemble, link, and run — all from a single `hla64` CLI.

[![Status](https://img.shields.io/badge/Fase%200%E2%80%939-Done%20%C2%B7%209.5-Active-2ea44f)](./docs/roadmap.md)
[![Tests](https://img.shields.io/badge/tests-65%2F65-2ea44f)](#test-status)
[![Target](https://img.shields.io/badge/target-linux--x64--sysv-1f6feb)](#target-abis)
[![Language](https://img.shields.io/badge/language-v0.1%20Draft-blueviolet)](#language-reference)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](#license)

---

## ✨ What's Working (MVP Linux x64)

| Area                | Status | Details |
|---------------------|--------|---------|
| **Lexer / Parser**  | ✅     | Full token set, recursive-descent parser, AST, source locations |
| **NASM Backend**    | ✅     | Emits valid ELF64 NASM with HLA→NASM operand-order flip |
| **Linux SysV ABI**  | ✅     | `_start`, exit code from `rax`, syscall conventions |
| **Stdlib**          | ✅     | `stdout.put("str", nl, rax, 42)` — strings, registers, integers |
| **Procedures**      | ✅     | Up to 6 integer args via `rdi`..`r9`, `@returns("rax")` |
| **Control flow**    | ✅     | `if/else/endif`, `while/endwhile` with `=`, `<`, `>` |
| **Local variables** | ✅     | `var` block, stack frame with `[rbp-N]` addressing |
| **CLI**             | ✅     | `build`, `emit-nasm`, `run`, plus `hla64 --version` |
| **Test runner**     | ✅     | Library + sample manifests, JSON-driven (CLI command Fase 9) |
| **Windows x64**     | 🔜     | Planned (Fase 11) |

> The MVP compiler inlines `sys_write` syscalls. A hand-written runtime
> library (`src/HlaX64.Runtime/`) is provided for Fase 6+ (procedure-aware
> compilation) and Fase 12 (C# interop shared library).

---

## 🚀 Quick Start

```bash
# 1. Build everything
dotnet build

# 2. Run all tests
dotnet test

# 3. Compile a .hla64 file to NASM
dotnet run --project src/HlaX64.Cli -- emit-nasm examples/hello.hla64

# 4. Build a .hla64 file into a Linux ELF executable
dotnet run --project src/HlaX64.Cli -- build examples/exitcode.hla64 -o build/exitcode

# 5. Compile and run
dotnet run --project src/HlaX64.Cli -- run examples/hello.hla64
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

More examples live under [`examples/`](./examples).

---

## 🏗 Project Structure

```text
HlaX64/
├─ src/
│  ├─ HlaX64.Compiler/        # Lexer, Parser, AST, Semantic, TestRunner lib
│  ├─ HlaX64.Backend.Nasm/    # NASM x64 code emitter
│  ├─ HlaX64.Cli/             # Command-line interface (hla64)
│  └─ HlaX64.Runtime/         # Linux x64 runtime library
│     ├─ include/             #   *.hhf header
│     └─ linux-x64/           #   *.nasm runtime sources
├─ tests/
│  ├─ HlaX64.Compiler.Tests/  # xUnit suite (65 tests)
│  └─ samples/                # Integration test samples with manifests
├─ docs/
│  ├─ language-spec.md        # Full language reference
│  └─ abi-linux-x64.md        # System V ABI call convention
├─ examples/                  # *.hla64 sample programs
└─ scripts/                   # build.ps1, test.ps1
```

---

## 🎯 Target ABIs

| Target              | Status         | Compiler flag        |
|---------------------|----------------|----------------------|
| `linux-x64-sysv`    | ✅ Implemented | (default)            |
| `windows-x64-msabi` | 🔜 Future      | (Fase 11)            |

See [`docs/abi-linux-x64.md`](./docs/abi-linux-x64.md) for the Linux
System V call convention details (argument registers, return register,
stack alignment, shadow space).

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
Passed!  - Failed: 0, Passed: 65, Skipped: 0, Total: 65
```

| Suite               | Count | Coverage |
|---------------------|-------|----------|
| `LexerTests`        | ~14   | Keywords, registers, literals, comments, positions |
| `ParserTests`       | ~7    | Programs, calls, includes, control flow, procedures |
| `NasmEmitterTests`  | ~20   | Instruction lowering, ABI, runtime markers, hello world |
| `SemanticAnalyzerTests` | ~7 | Register & instruction validation, diagnostics |
| `TestRunnerTests`   | ~8    | Manifest loading, runner flow, source resolution |
| **Total**           | **65** | All passing ✅ |

---

## 🗺 Roadmap

Phases from [`HlaX64_Project_Plan.md`](./HlaX64_Project_Plan.md) (konsolidasi).
Detail per fase ada di plan; ringkasan visual ada di
[`docs/roadmap.md`](./docs/roadmap.md).

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
| **9.5** | **Compiler Architecture Stabilization (IR, ABI lowerer, type system, runtime contract, native tests)** | 🛠 **Active** |
| 10   | Benchmark runner                     | ⏳      |
| 11   | Windows x64 backend                  | ⏳      |
| 12   | C ABI & C# interop generator         | ⏳      |
| 13   | MCP server (for AI agents)           | ⏳      |
| 14   | LSP & editor tooling                 | ⏳      |
| 15   | AI Assembly Lab / IDE plugin         | ⏳      |

> **Aturan**: jangan sentuh Fase 10–15 sebelum 15-item Definition of Done
> Fase 9.5 terpenuhi. Lihat [`HlaX64_Project_Plan.md` §9.5](./HlaX64_Project_Plan.md).

### 📚 Dokumentasi tambahan

- [`docs/roadmap.md`](./docs/roadmap.md) — peta fase + Tier eksekusi aktif.
- [`docs/compiler-architecture.md`](./docs/compiler-architecture.md) — diagram pipeline IR + 7 workstream.
- [`docs/runtime-contract.md`](./docs/runtime-contract.md) — format clobber metadata + kontrak SysV/Windows.
- [`docs/examples.md`](./docs/examples.md) — katalog program contoh & cara menjalankan.

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

## 📄 License

MIT
