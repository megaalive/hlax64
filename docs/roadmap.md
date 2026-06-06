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
| 14 | LSP & editor tooling | ✅ MVP | diagnostics, hover, completion, go-to-definition, document symbols, format-on-save; VS Code client |
| 15 | AI Assembly Lab / GUI | ⏳ Pending | eksperimen UI (Avalonia/WPF) |
| **16** | **Language core completion** | 🚧 Sprint 1 | const blocks, expressions (planned), enum, struct, `sizeof`, string model |
| 17 | ABI and FFI completion | ⏳ | stack args, float ABI, function pointers, external libs |
| 18 | Compiler verification | ⏳ | definite assignment, CFG/stack/ABI verifiers, fuzz expansion |
| 19 | Debug and explainability | ⏳ | source map, DWARF MVP, disassembly, trace mode |
| 20 | Optimization | ⏳ | const fold/prop, DCE, peephole, regalloc |
| 21 | CPU and SIMD | ⏳ | instruction DB, SSE2/AVX2, intrinsics, atomics |
| 22 | Modules and packages | ⏳ | manifest, deps, incremental/reproducible builds |
| 23 | Verified executable workflow | ⏳ | proof bundle, capability manifest, semantic diff |
| 24 | Debugger and Assembly Lab | ⏳ | DAP, registers/stack/memory views, AI repair |

### Phase 16 sprint notes

- **Sprint 1 (done):** docs sync, RFC 0004, `const`/`endconst`, compile-time expressions, hex `$FF`
- **Sprint 2+:** runtime `:=` expressions, enum, struct, global data

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
> Next: Phase 16 Sprint 2 (runtime expressions), then Fase 15 (GUI / AI Assembly Lab).

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
