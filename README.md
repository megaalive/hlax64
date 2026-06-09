# HlaX64 — HLA-Inspired x64 Assembly Layer for Vibe Coding

> **AI-friendly x64 assembly source layer for verified executable vibe coding.**
> Write low-level x64 with a cleaner HLA-inspired syntax. Compile to NASM,
> assemble, link, and run — all from a single `hla64` CLI.

[![Status](https://img.shields.io/badge/HlaX64%200.2%20Alpha-green)](./docs/roadmap.md)
[![Tests](https://img.shields.io/badge/tests-CI%20verified-2ea44f)](https://github.com/megaalive/hlax64/actions)
[![Target](https://img.shields.io/badge/target-linux--x64--sysv%20|%20windows--x64--msabi-1f6feb)](#targets)
[![Language](https://img.shields.io/badge/language-v0.1%20Draft-blueviolet)](#language-reference)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue)](#license)
[![Pages](https://img.shields.io/badge/docs-GitHub%20Pages-2ea44f)](https://megaalive.github.io/hlax64/)

## Try it now

👉 **[Open HlaX64 Playground](https://megaalive.github.io/hlax64/playground/index.html?example=argv)**

Inspect HlaX64 source, **generated NASM**, line notes, IR summary, and **AI debug / explain / optimize** prompts — no install required.

The browser playground uses **cached compile artifacts** for **181** curated examples (pre-generated with `hla64 explain --json`). Editing source in the browser does not recompile; use the **Run locally** command in the playground or the CLI below for live builds.

### Try examples in browser

- [argv](https://megaalive.github.io/hlax64/playground/index.html?example=argv) — command-line arguments (`hlax_argv_*`)
- [filemagic](https://megaalive.github.io/hlax64/playground/index.html?example=filemagic) — sniff file type from magic bytes
- [wc](https://megaalive.github.io/hlax64/playground/index.html?example=wc) — lines, words, bytes
- [hexdump](https://megaalive.github.io/hlax64/playground/index.html?example=hexdump) — hex + ASCII dump
- [hello](https://megaalive.github.io/hlax64/playground/index.html?example=hello) · [linecount](https://megaalive.github.io/hlax64/playground/index.html?example=linecount) · [All examples in playground](https://megaalive.github.io/hlax64/playground/)

For **live compilation**, assembly, link, and run:

```bash
hla64 doctor
hla64 run examples/curriculum/00-getting-started/hello.hla64
hla64 explain examples/tools/10-windows/exists/exists.hla64 --json
```

---

## Install

New users should start with the **Assembly Lab** bundle: desktop UI, embedded terminal, CLI, runtime files, examples, and toolchain Settings in one package. CLI-only archives, global tool, or clone workflows are also documented in **[docs/install.md](docs/install.md)**.

Tutorials: [Getting started](docs/tutorials/01-getting-started.md) · [**Patterns & cookbook**](docs/patterns.md) · [Native routines](docs/tutorials/02-native-routines.md) · [Memory & pointers](docs/tutorials/05-memory.md) · [C# interop](docs/tutorials/03-csharp-interop.md) · [MCP agents](docs/tutorials/04-mcp-agent.md) · [Assembly Lab](docs/tutorials/06-assembly-lab.md)

> **Build failing or wrong results?** Read **[docs/patterns.md](docs/patterns.md)** — operand order, exit codes (`rbx` on Windows), `idiv`/`mod`, static arrays, and common NASM/link errors.

---

## Current Capabilities — HlaX64 0.2 Alpha

HlaX64 is an **early multi-platform x64 language toolchain** — not a single-target MVP. Release [`v0.2.1-alpha`](https://github.com/megaalive/hlax64/releases/tag/v0.2.1-alpha) ships pre-built CLI and Assembly Lab archives; CI verifies every push.

### Language

| Area | Status | Notes |
|------|--------|-------|
| Program / procedure structure | ✅ | `program`, `procedure`, `begin`/`end`, `#include` |
| Types & registers | ✅ | `int64`, `uint64`, `byte`, `ptr`, …; full x64 register set |
| Instructions | ✅ | `mov`, `add`, `sub`, `imul`, bitwise, `cmp`, … (HLA operand order) |
| Control flow | ✅ | `if`/`else`/`endif`, `while`/`endwhile` |
| Locals & stack arrays | ✅ | `var` block; `type[N]` including packed `byte[N]` |
| Pointers & indexing | ✅ | `&var`, `&"str"`, `[reg+N]`, `arr[i]` — [RFC 0002](rfcs/0002-pointer-model.md) |
| Compile-time constants | ✅ | `const` / `endconst`, `:=`, expr eval — [RFC 0004](rfcs/0004-expressions-and-constants.md) |
| Runtime expressions `:=` | ✅ | Int64 locals/registers — [RFC 0004](rfcs/0004-expressions-and-constants.md) |
| Enums | ✅ | `enum`/`endenum`, auto-increment members — [RFC 0005](rfcs/0005-enum-model.md) |
| Records | ✅ | `record`/`endrecord`, `packed`, field access, `sizeof`/`offsetof` — [RFC 0006](rfcs/0006-struct-layout.md) |
| Static / global data | ✅ | `static`/`endstatic`, `.data`/`.bss` — [RFC 0007](rfcs/0007-global-data.md) |
| String model | ✅ | `cstring` alias, built-in `utf8slice` — [RFC 0008](rfcs/0008-string-model.md) |

### Compiler

| Area | Status | Notes |
|------|--------|-------|
| Pipeline | ✅ | Lexer → Parser → Semantic → IR → ABI lowerer → NASM |
| Diagnostics | ✅ | Line/column, codes HLAX00xx, fuzzy suggestions; multi-error parse recovery (extern signatures, statement bodies) |
| Bounds warnings | ✅ | `-Wbounds` / HLAX0030 for static array indices |
| Verification warnings | ✅ | `-Wverify` / HLAX0060–63 (definite assignment, CFG, liveness) |
| Stack / ABI verify | ✅ | `hla64 verify-stack`, `hla64 verify-abi` |
| Format | ✅ | `hla64 format` via `AstFormatter` |

### Targets

| Target | Flag | Output |
|--------|------|--------|
| Linux x64 SysV | (default) | ELF exe, `.so` shared library |
| Windows x64 MS ABI | `--target windows-x64-msabi` | `.exe`, `.dll` |

### Interop

| Area | Status |
|------|--------|
| C ABI `export procedure` | ✅ |
| `hla64 generate-header` | ✅ |
| `hla64 generate-pinvoke` | ✅ |

### Tooling

| Area | Status |
|------|--------|
| CLI (`build`, `run`, `test`, `bench`, `explain`, `doctor`, `disasm`, `diff`, `plan`, …) | ✅ |
| **Assembly Lab** (Avalonia desktop — source, IR/NASM/ABI, build/run, proof bundle, **embedded PTY terminal**, toolchain **Settings**) | ✅ |
| Source maps / proof bundle / `--optimize O0-O2` / CPU+AVX2 / `simd.*`+`atomic.*` / dependency restore / DAP debug / `test-differential` | ✅ |
| MCP server (12 tools, stdio JSON-RPC) | ✅ |
| Benchmark runner + JSON manifests | ✅ |

### Agent integration

Designed for verified vibe coding: explicit syntax, structured diagnostics, annotated NASM (`; RUNTIME:`), JSON CLI schemas, MCP compile/build/run/test tools. See [tutorials/04-mcp-agent.md](docs/tutorials/04-mcp-agent.md).

### Editor support

| Feature | Status |
|---------|--------|
| LSP (diagnostics, hover, completion, go-to-definition, symbols, format, signature help, semantic tokens, highlights, virtual IR/NASM/stack+ABI) | ✅ |
| VS Code extension (`editors/vscode`) | ✅ syntax + LSP client |

### Testing

| Suite | Role |
|-------|------|
| `dotnet test` | Unit, parser, semantic, IR, conformance, LSP |
| `hla64 test` | Native Linux integration (compile → link → run) |
| Example compile guards | Every `examples/**/*.hla64` must emit NASM |
| Curriculum manifests | Structured learning path under `examples/` |

All suites run in [GitHub Actions CI](https://github.com/megaalive/hlax64/actions).

---

## 🚀 Quick Start

```bash
# 1. Build everything
dotnet build

# 2. Run all tests
dotnet test

# 3. Compile a .hla64 file to NASM
dotnet run --project src/HlaX64.Cli -- emit-nasm examples/curriculum/00-getting-started/hello.hla64

# 4. Build a .hla64 file into a Linux ELF executable
dotnet run --project src/HlaX64.Cli -- build examples/curriculum/00-getting-started/exitcode.hla64 -o build/exitcode

# 5. Compile and run
dotnet run --project src/HlaX64.Cli -- run examples/curriculum/00-getting-started/hello.hla64

# 6. Launch Assembly Lab (Phase 15)
dotnet run --project src/HlaX64.AssemblyLab
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

## Project layout

```text
src/          Compiler, NASM backend, CLI, runtime, MCP, LSP
tests/        Unit tests, native samples, conformance, curriculum manifests
examples/     Topic-organized .hla64 programs
docs/         Language spec, ABI, tutorials, architecture
rfcs/         Language design proposals
editors/      VS Code extension
```

Pipeline details: [`docs/compiler-architecture.md`](./docs/compiler-architecture.md).

---

## 📖 Language at a Glance

- **Operand order** follows HLA: `mov(source, dest)`. The NASM backend
  reverses it to NASM's `mov dest, source` automatically.
- **Program structure** mirrors classic HLA:
  `program name; begin name; … end name;`
- **Standard library** ships file I/O (`hlax_file_*`, `hlax_path_exists`), string/memory helpers, stdout, argv, heap — see [`stdlib64.hhf`](./src/HlaX64.Runtime/include/stdlib64.hhf).
- **See [`docs/language-spec.md`](./docs/language-spec.md)** for the
  complete reference: types, registers, instructions, control flow,
  procedures, and escape sequences.

---

## Test status

```bash
dotnet test          # unit + conformance (+ example compile guards in CI)
dotnet run --project src/HlaX64.Cli -- test   # native integration on Linux
```

CI runs the full matrix on every push; see the [Actions tab](https://github.com/megaalive/hlax64/actions) for current pass/fail status. Windows CI includes MS ABI build smoke tests; native run assertions execute on Linux.

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
| 14   | LSP & editor tooling                 | ✅ Hardened |
| 15   | AI Assembly Lab (Avalonia desktop)   | ✅ Done |
| 16   | Language core (const, expr, enum, record, static) | ✅ Done |
| 17   | ABI & FFI (extern, fn ptr, float, struct param) | ✅ Done |
| 18   | Compiler verification                | ✅ Done |
| 19   | Debug & explainability               | ✅ Hardened |
| 20   | Optimization (O1/O2)                 | ✅ Hardened |
| 21   | CPU & SIMD                           | ✅ Hardened |
| 22   | Modules & packages                   | ✅ Hardened |
| 23   | Verified executable workflow         | ✅ Hardened |
| 24   | Debugger & Assembly Lab integration  | ✅ Hardened |

Detail per sprint: [`docs/roadmap.md`](./docs/roadmap.md). Yang masih **planned** (bukan fase 16 inti) — typed pointers, `slice<T>`, `idiv`/`jmp` sebagai instruksi sumber, full DWARF Windows.

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

- [Contributing Guide](docs/rules/CONTRIBUTING.md) — how to build, test, and submit changes
- [Install Guide](docs/install.md) — Assembly Lab bundle, CLI archives, and global tool
- [Development Guide](docs/development.md) — local setup and CI parity
- [Governance](docs/rules/GOVERNANCE.md) — decision process and labels
- [Issue backlog](docs/issue-backlog.md) — triaged open/closed GitHub issues
- [Code of Conduct](docs/rules/CODE_OF_CONDUCT.md) — our community standards
- [Security Policy](docs/rules/SECURITY.md) — how to report vulnerabilities
- [Support](docs/rules/SUPPORT.md) — documentation and getting help
- [VS Code extension](editors/vscode/README.md) — syntax highlighting for `.hla64`
- [Open Issues](https://github.com/megaalive/hlax64/issues) — curated good-first and help-wanted tasks
- [MCP Tools](docs/mcp-tools.md) — agent tool catalog
- [Changelog](CHANGELOG.md) — release history

## 📄 License

MIT
