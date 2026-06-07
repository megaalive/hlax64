# HlaX64 — Roadmap

> **Status dokumen**: Aktif · selaras dengan `HlaX64_Project_Plan.md` (konsolidasi)
> **Bahasa**: `HlaX64` v0.1 (Draft Language Reference)
> **Target aktif**: `linux-x64-sysv`

Dokumen ini adalah ringkasan eksekusi yang bisa dibaca cepat. Untuk
detail lengkap (rationale, deliverable, acceptance criteria), lihat
[`HlaX64_Project_Plan.md`](../HlaX64_Project_Plan.md).

---

## 1. Peta fase

| Fase | Nama | Status | Target |
|------|------|--------|--------|
| 0 | Foundation & repo setup | ✅ Done | repo, solution, CLI `--version` |
| 1 | Lexer & Parser MVP | ✅ Done | program, procedure, call |
| 2 | NASM Backend MVP | ✅ Done | NASM x64 valid, operand order |
| 3 | Toolchain build Linux x64 | ✅ Done | `hla64 build`, `hla64 run` |
| 4 | Runtime `stdout.put` | ✅ Done | syscall/inline write |
| 5 | Semantic Analyzer | ✅ Done | validasi register, instruksi, scope |
| 6 | Procedure & SysV ABI | ✅ Done | 1–6 args, `rdi..r9`, return `rax` |
| 7 | Control flow | ✅ Done | `if/else/endif`, `while/endwhile` |
| 8 | Local variables & stack frame | ✅ Done | `var` block, `[rbp-N]` |
| 9 | Test runner | ✅ Done | `hla64 test`, manifest JSON |
| **9.5** | **Compiler Architecture Stabilization** | ✅ Done | A ✅ B ✅ C ✅ D ✅ E ✅ F ✅ G ✅ H ✅ — 15/15 checklist |
| 10 | Benchmark runner | ✅ Done | `hla64 bench` dengan warmup, median, compile duration, binary size, JSON manifest |
| 11 | Windows x64 backend | ✅ Done | RCX/RDX/R8/R9, 32-byte shadow space, `ExitProcess`, `WriteConsoleA`, `--target windows-x64-msabi` |
| 12 | C ABI & C# interop | ✅ Done | `export procedure`, `--output-kind shared-library`, C header generator, C# P/Invoke generator |
| 13 | MCP server | ✅ Done | 9 tools via stdio JSON-RPC (compile, build, run, test, explain-abi, dll) |
| 14 | LSP & editor tooling | ✅ Hardened | + signatureHelp, highlights/references, semanticTokens, format codeAction |
| 15 | AI Assembly Lab / GUI | ✅ Done | Avalonia 11 desktop Lab — see RFC 0024 |
| **16** | **Language core completion** | ✅ Done | const, expressions, enum, record, static, string model |
| 17 | ABI and FFI completion | ✅ Done | Sprints 2–5: extern, fn ptr, float ABI, struct param, variadic RFC |
| 18 | Compiler verification | ✅ Done | definite assignment, CFG, liveness, verify-stack/abi, fuzz |
| 19 | Debug and explainability | ✅ Hardened | source map lookup, DWARF file table, disasm+objdump, `--trace` int3 |
| 20 | Optimization | ✅ Hardened | O1 fold, O2 propagation + xor-zero peephole |
| 21 | CPU and SIMD | ✅ Hardened | HLAX0071 + instruction DB (34 mnemonics), AVX2 gates |
| 22 | Modules and packages | ✅ Hardened | `hla64.lock`, manifest `build` |
| 23 | Verified executable workflow | ✅ Hardened | capabilities extern/stdout, diff stack/return, proof-bundle tests |
| 24 | Debugger and Assembly Lab | ✅ Hardened | DAP setBreakpoint/stackTrace stub, stack clobber doc, MCP abiIssues |

### Phase 15 — Assembly Lab (done)

Avalonia desktop app `HlaX64.AssemblyLab` — visual pipeline shell (source, IR/NASM/ABI, build/run, source map sync, DAP MVP, proof bundle). See [RFC 0024](../rfcs/0024-assembly-lab.md) and [tutorial 06](tutorials/06-assembly-lab.md).

- **Sprint 15.1 (done):** Avalonia 11 + MVVM shell, `AssemblyLabBackend.Compile`, open file/folder, live debounced diagnostics, CI build
- **Sprint 15.2 (done):** IR/NASM/ABI tabs, Build/Run, target selector (`linux-x64-sysv` / `windows-x64-msabi`)
- **Sprint 15.3 (done):** `.hlamap.json` load, diagnostic → NASM line highlight
- **Sprint 15.4 (done):** Debug button + DAP output panel (MVP; Linux gdb via CLI follow-up)
- **Sprint 15.5 (done):** Proof bundle export + capabilities panel
- **Sprint 15.6 (done):** RFC 0024, tutorial, docs
- **Deferred:** AvaloniaEdit + gutter breakpoints, Windows lldb DAP, embedded MCP explain client, disasm pane, release RID packaging

---

### Phase 16 sprint notes

- **Sprint 1 (done):** docs sync, RFC 0004, `const`/`endconst`, compile-time expressions, hex `$FF`
- **Sprint 2 (done):** runtime `:=` expressions for int64 scalars
- **Sprint 3 (done):** `enum`/`endenum`, typed backing, `Color.Red` immediates (RFC 0005)
- **Sprint 4 (done):** `record`/`endrecord`, natural layout, field access, `sizeof`/`offsetof` (RFC 0006)
- **Sprint 5 (done):** `static`/`endstatic`, `.data`/`.bss`, `cstring`, `utf8slice` (RFC 0007/0008)
- **Sprint 5+ (done):** `packed` records, procedure-scoped enum/record, enum auto-increment

### Phase 17 sprint notes

- **Sprint 1 (done):** stack arguments beyond register limit (SysV 7+, Windows 5+); RFC 0009; example `stack-args-sysv.hla64`
- **Sprint 2 (done):** `extern procedure` + `from "lib"` link hints; RFC 0010; example `extern-puts.hla64`; HLAX0050+
- **Sprint 3 (done):** function pointer type aliases + indirect `call`; RFC 0011; example `indirect-call.hla64`
- **Sprint 4 (done):** float32/float64 param/return MVP; RFC 0012; example `float-return.hla64`
- **Sprint 5 (done):** record param as hidden pointer; variadic extern RFC + HLAX0055; RFC 0013; example `record-param.hla64`

### Phase 18 sprint notes

- **Sprint 1 (done):** definite assignment analysis; HLAX0060; `-Wdefinite`; LSP default warning
- **Sprint 2 (done):** unreachable code HLAX0061; missing `@returns` path HLAX0062; `-Wunreachable`
- **Sprint 3 (done):** caller-saved register liveness across `call` HLAX0063; `-Wliveness`
- **Sprint 4 (done):** `hla64 verify-stack`; `StackVerifier.cs`; HLAX0064–68; RFC 0014
- **Sprint 5 (done):** `hla64 verify-abi`; per-procedure ABI report (params, return, externs)
- **Sprint 6 (done):** fuzz tests (UTF-8 lexer, formatter round-trip, manifest JSON); `docs/development.md`
- **Sprint 7 (done):** differential testing for `examples/01-arithmetic/simple.hla64` (curriculum manifest exit code 3)

### Phase 19 sprint notes

- **Sprint 1 (MVP):** `*.hlamap.json` via `--source-map`; IR id annotations; RFC 0015
- **Sprint 2 (MVP):** `--debug-info` NASM `%line` + `.debug_line` stub (Linux); RFC 0016
- **Sprint 3 (MVP):** `hla64 disasm`; `--trace` procedure entry/exit + Linux `int3` breakpoint
- **Hardening:** `SourceMapDocument.LookupBySource/NasmLine/IrId`; DWARF file table stub; objdump merge when on PATH
- **Deferred:** full DWARF on Windows, runtime trace sink

### Phase 20 sprint notes

- **Sprint 1 (MVP):** `--optimize O0|O1|O2`; IR constant folding + copy propagation (O2); RFC 0017
- **Sprint 2 (MVP):** peephole `mov reg,reg`, `add reg,0`, `mov reg,0` → `xor reg,reg` (O2)
- **Deferred:** `--register-mode assisted`, global DCE, propagation

### Phase 21 sprint notes

- **Sprint 1 (MVP):** `data/instructions.json`; `hla64 list-instructions`; RFC 0018
- **Advanced hardening round 2 (done):** AVX2 codegen; simd/atomic intrinsics; dependency resolver; DAP; variadic printf; differential CI; 308 tests
- **Sprint 2 (MVP):** `--cpu` / `--features`; HLAX0070/0071; SSE2 + AVX2; intrinsics RFC 0019/0020 partial
- **Deferred:** typed f64x4, cmpxchg atomics, Windows ymm save/restore

### Phase 22 sprint notes

- **Sprint 1 (MVP):** `hla64.toml` schema; `hla64 new console`; RFC 0021
- **Sprint 2 (MVP):** `hla64 restore` resolves path deps; lock verification on build; RFC 0021 partial
- **Sprint 3 (MVP):** `hla64 verify-reproducible`; `hla64.lock` schema documented

### Phase 23 sprint notes

- **Sprint 1 (MVP):** `--proof-bundle`; `capabilities.json`; RFC 0022
- **Sprint 2 (MVP):** `hla64 diff`; `hla64 plan --json`

### Phase 24 sprint notes

- **Sprint 1 (MVP):** `hla64 debug --stdio` DAP + gdb (Linux); RFC 0023 partial
- **Sprint 2 (MVP):** LSP virtual IR/NASM/stack; VS Code commands
- **Sprint 3 (MVP):** MCP `explain` structured `suggestedFix`
- **Deferred:** Windows DAP/lldb parity, Lab editor syntax highlight / gutter breakpoints, embedded MCP client

---

## 2. Tier eksekusi saat ini

### Tier 1 — Dokumentasi (zero risk)

- [x] `docs/roadmap.md` (file ini)
- [x] `docs/compiler-architecture.md` — diagram pipeline IR + 7 workstream
- [x] `docs/runtime-contract.md` — format clobber metadata per fungsi runtime
- [x] `docs/examples.md` — katalog & cara menjalankan
- [x] `README.md` — sync badge "Fase 0–13 Done"

### Tier 2 — Sample tests (memenuhi target MVP "min 10 samples")

✅ **Sudah 20 sample** — native integration tests under `tests/samples/` (incl. Level 3 memory).

### Tier 3 — Mini CLI command (mitigasi Risiko 2)

- [x] `hla64 explain-abi --target linux-x64-sysv` ✅

---

## 3. Setelah 9.5 selesai

Urutan roadmap yang direkomendasikan (lihat Plan Section 9):

```
Fase 9.5 →  Fase 11 (Windows) →  Fase 12 (C# interop) →  Fase 10 (Bench)
Fase 13 (MCP) →  Fase 14 (LSP) →  Fase 15 (GUI)
```

> *Fase 9.5 Workstream A–H done. Docs sync complete. Fase 10 (Bench), 11 (Windows), 12 (C# interop), 13 (MCP) all done.
> **Level 3 memory curriculum** (RFC 0002/0003) — pointers, indexed `[reg+N]`, stack arrays `type[N]` (incl. packed `byte[N]`), string walk, optional `-Wbounds` (HLAX0030).
> **Fase 14 LSP MVP** — diagnostics (incl. bounds warnings), hover, completion, go-to-definition, document symbols, format-on-save; VS Code language client.
> **Phase 16 complete** — language core (const, expressions, enum, record, static, cstring/utf8slice). **Phase 17 complete** — ABI/FFI (extern, fn ptr, float, struct param). **Phase 18 complete** — verification (definite assignment, CFG, liveness, verify-stack/abi). Next: Phase 19 (debug/explainability) or Fase 15 (GUI / AI Assembly Lab).

---

## 4. Prioritas fitur (ringkas)

Lihat Plan Section 10 untuk detail.

- **P0 (wajib)**: parser, emitter, CLI, hello, procedure, test runner, **stabilisasi 9.5**.
- **P1 (penting)**: control flow, local vars, semantic, **type system eksplisit, IR, ABI lowerer, native test**, Windows, C# interop.
- **P2 (menarik)**: MCP, benchmark, LSP, `explain-abi`/`explain-error`, VS Code, GUI.
- **P3 (nanti)**: macro, LLVM, SIMD, optimizer, OSDev, full HLA compatibility, JIT, self-hosting.

---

## 5. Risiko aktif

Lihat Plan Section 11.

- **Risiko 6 (termitigasi)**: pertumbuhan fitur sebelum fondasi stabil.
  → Mitigasi: **Fase 9.5 completed** — IR, ABI lowerer, native tests, runtime contract, docs all stable.
