# HlaX64 — Roadmap

> **Status dokumen**: Aktif · selaras dengan `HlaX64_Project_Plan.md` (konsolidasi lokal)
> **Bahasa**: `HlaX64` v0.1 (Draft Language Reference)
> **Target aktif**: `linux-x64-sysv`

Dokumen ini adalah ringkasan eksekusi yang bisa dibaca cepat. Untuk
detail lengkap (rationale, deliverable, acceptance criteria), lihat
[`HlaX64_Project_Plan.md`](../HlaX64_Project_Plan.md) bila tersedia di clone lokal.

### Legenda status

| Label | Arti |
|-------|------|
| **✅ Done** | Scope fase selesai; ada di toolchain dan contoh/tes. |
| **✅ Hardened** | MVP fase sudah shipped; dilanjutkan hardening (lookup, stub, CI, docs). |
| **🔜 Deferred** | Belum dikerjakan; lihat [§6 Backlog terbuka](#6-backlog-terbuka-deferred). |

> **Catatan:** Istilah *MVP* di RFC/commit lama hanya berarti *scope minimum yang
> pernah dijanjikan*, bukan “masih belum ada”. Jika sprint tercantum **(done)**,
> fiturnya sudah ada meski belum “versi final”.

---

## 1. Peta fase

| Fase | Nama | Status | Ringkasan |
|------|------|--------|-----------|
| 0 | Foundation & repo setup | ✅ Done | repo, solution, CLI `--version` |
| 1 | Lexer & Parser | ✅ Done | program, procedure, call |
| 2 | NASM Backend | ✅ Done | NASM x64 valid, operand order |
| 3 | Toolchain build Linux x64 | ✅ Done | `hla64 build`, `hla64 run` |
| 4 | Runtime `stdout.put` | ✅ Done | syscall / library write |
| 5 | Semantic Analyzer | ✅ Done | register, instruksi, scope |
| 6 | Procedure & SysV ABI | ✅ Done | `rdi..r9`, return `rax` |
| 7 | Control flow | ✅ Done | `if/else/endif`, `while/endwhile` |
| 8 | Local variables & stack frame | ✅ Done | `var`, `[rbp-N]` |
| 9 | Test runner | ✅ Done | `hla64 test`, manifest JSON |
| **9.5** | **Compiler Architecture Stabilization** | ✅ Done | IR, ABI lowerer, native tests, runtime contract |
| 10 | Benchmark runner | ✅ Done | `hla64 bench`, warmup, median, JSON manifest |
| 11 | Windows x64 backend | ✅ Done | MS ABI, shadow space, `ExitProcess` |
| 12 | C ABI & C# interop | ✅ Done | export DLL, header / P/Invoke generator |
| 13 | MCP server | ✅ Done | stdio JSON-RPC tools (compile, build, run, …) |
| 14 | LSP & editor tooling | ✅ Hardened | diagnostics, hover, completion, semantic tokens |
| 15 | AI Assembly Lab | ✅ Done | Avalonia Lab — [RFC 0024](../rfcs/0024-assembly-lab.md) |
| 16 | Language core | ✅ Done | const, expr, enum, record/struct, static, strings |
| 17 | ABI & FFI | ✅ Done | extern, fn ptr, float, record param, variadic |
| 18 | Compiler verification | ✅ Done | definite assignment, CFG, liveness, verify-* |
| 19 | Debug & explainability | ✅ Hardened | source map, DWARF stub Linux, disasm, `--trace` |
| 20 | Optimization | ✅ Hardened | `--optimize O0\|O1\|O2`, fold, peephole |
| 21 | CPU & SIMD | ✅ Hardened | instruction DB, `--features`, AVX2 intrinsics |
| 22 | Modules & packages | ✅ Hardened | `hla64.toml`, lockfile, restore |
| 23 | Verified executable workflow | ✅ Hardened | proof bundle, capabilities, diff/plan |
| 24 | Debugger & Assembly Lab | ✅ Hardened | DAP, Lab debug/MCP integration |

**Semua fase 0–24 sudah minimal Done/Hardened.** Pekerjaan lanjutan hanya item **Deferred** (§6).

---

## 2. Catatan sprint (riwayat — semua shipped)

### Phase 15 — Assembly Lab

- **15.1–15.6 (done):** shell Avalonia, IR/NASM/ABI tabs, build/run, source map, DAP panel, proof bundle, RFC + tutorial
- **Batch 3–4 (done):** gdb/lldb DAP, Explain/Agent tab, Apply Fix, MI registers, MCP tab

### Phase 16 — Language core

- **Sprint 1–5+ (done):** const, runtime `:=`, enum, record, static, cstring/utf8slice, packed, scoped types

### Phase 17 — ABI & FFI

- **Sprint 1–5 (done):** stack args, extern+link, indirect call, float return, record param, variadic extern

### Phase 18 — Verification

- **Sprint 1–7 (done):** `-Wdefinite`, unreachable, liveness, `verify-stack`/`verify-abi`, fuzz, differential CI

### Phase 19 — Debug (Hardened)

- **Sprint 1 (done):** `--source-map`, `*.hlamap.json`, IR id annotations — RFC 0015
- **Sprint 2 (done):** `--debug-info`, NASM `%line`, `.debug_line` stub (Linux) — RFC 0016
- **Sprint 3 (done):** `hla64 disasm`, `--trace`, Linux `int3` entry breakpoints
- **Hardening (done):** `SourceMapDocument` lookup, DWARF file table stub, objdump merge

### Phase 20 — Optimization (Hardened)

- **Sprint 1 (done):** `--optimize O0|O1|O2`, constant folding, copy propagation (O2) — RFC 0017
- **Sprint 2 (done):** peephole `mov reg,reg`, `add reg,0`, `xor` zeroing (O2)

### Phase 21 — CPU & SIMD (Hardened)

- **Sprint 1 (done):** `data/instructions.json`, `hla64 list-instructions` — RFC 0018
- **Sprint 2 (done):** `--cpu` / `--features`, HLAX0070/0071, SSE2/AVX2, partial intrinsics — RFC 0019/0020
- **Hardening round 2 (done):** AVX2 codegen, simd/atomic builtins, variadic printf, expanded CI

### Phase 22 — Packages (Hardened)

- **Sprint 1–3 (done):** `hla64.toml`, `hla64 new`, restore, lock verification, `verify-reproducible` — RFC 0021

### Phase 23 — Verified workflow (Hardened)

- **Sprint 1–2 (done):** `--proof-bundle`, `capabilities.json`, `hla64 diff`, `hla64 plan --json` — RFC 0022

### Phase 24 — Debugger & Lab (Hardened)

- **Sprint 1 (done):** `hla64 debug --stdio`, DAP + gdb (Linux) — RFC 0023 partial
- **Sprint 2 (done):** LSP virtual IR/NASM/stack, VS Code commands
- **Sprint 3 (done):** MCP `explain` + structured `suggestedFix`

---

## 3. Snapshot saat ini

| Area | Status |
|------|--------|
| Fase compiler 0–24 | ✅ Done / Hardened (lihat §1) |
| Kurikulum contoh | 58 manifest di `tests/examples-curriculum/` + real-tools / bug-farm / invalid |
| Tes otomatis | 400+ (`dotnet test` — compiler + AssemblyLab) |
| Target build | Linux SysV + Windows MS ABI |
| Dokumentasi produk | `docs/language-spec.md`, tutorials, RFC 0001–0024 |

**Fokus berikutnya:** **v0.2.0-alpha** (§5) — onboarding & playground, bukan fitur bahasa baru.

---

## 5. v0.2.0-alpha — Useful Assembly Tools & Onboarding

> **Tema release:** Playground sebagai produk utama GitHub Pages — orang buka link →
> coba contoh → lihat NASM → paham manfaat → baru clone repo.

| Item | Status | Catatan |
|------|--------|---------|
| Playground example picker (206 programs, nested by `examples/` category) | ✅ Done | `docs/playground/manifest.json`, `?example=wc` |
| Generated NASM pane (cached `explain --json`) | ✅ Done | `docs/playground/cache/`, `scripts/generate-playground-{manifest,cache}.ps1` |
| Explain-this-line (heuristic tutor) | ✅ Done | Klik baris + tab penjelasan |
| Copy AI Debug / Explain / Optimize prompt | ✅ Done | Playground tab AI |
| Live explain API (edit source → fresh NASM) | 🔜 Planned | `playground-design.md` Phase 2 |
| Monaco + syntax highlighting | 🔜 Planned | Reuse `hla64.tmLanguage.json` |
| Gallery deep links dari README | ✅ Done | `/playground?example=hexdump`, dll. |
| GitHub Pages sebagai “produk” (Home / Playground / Course / …) | ✅ Done | `docs/index.html` nav |
| Contoh `cat`, `strings`, `printf` di kurikulum | 🔜 Planned | `wc`/`hexdump`/`filemagic` sudah ada |
| Tutorial argv + file I/O path | 🔜 Planned | Perluas tutorials / course doc |
| Assembly Lab parity di browser (build/run) | 🔜 Deferred | Lab desktop tetap surface penuh |

**Sudah ada (jangan duplikasi):** Assembly Lab (IR/NASM live, Explain, Agent, DAP), MCP
`explain`, 58 manifest kurikulum, real-tools Windows + Linux ports.

**Non-goals v0.2:** macro, LLVM, `ptr<T>`, per-file `#pragma target` — tetap §6.

---

## 4. Tier eksekusi (historis)

Tier ini mencatat milestone awal proyek; semua sudah ✅.

### Tier 1 — Dokumentasi

- [x] `docs/roadmap.md`, `docs/compiler-architecture.md`, `docs/runtime-contract.md`, `docs/examples.md`
- [x] `README.md` — fase 0–24 Done/Hardened (bukan hanya 0–13)

### Tier 2 — Sample & kurikulum

- [x] Native integration tests (`tests/samples/`, `tests/examples-curriculum/`)
- [x] Real-tools, bug-farm, invalid mirrors, Linux ports

### Tier 3 — CLI utilitas

- [x] `hla64 explain-abi`, `explain`, `verify-*`, `bench`, MCP tools

---

## 5. Prioritas fitur (ringkas)

| Tier | Isi | Status |
|------|-----|--------|
| **P0** | parser, emitter, CLI, test runner, stabilisasi 9.5 | ✅ |
| **P1** | control flow, types, IR/ABI, Windows, C# interop | ✅ |
| **P2** | MCP, LSP, benchmark, Assembly Lab | ✅ |
| **P3 / Deferred** | macro, LLVM, OSDev, full HLA compat, JIT, self-hosting, §6 | 🔜 |

Detail prioritas lama: Plan Section 10 (lokal).

---

## 6. Backlog terbuka (Deferred)

Satu-satunya pekerjaan yang **belum** di roadmap aktif:

### Debug & explainability (19)

- Full DWARF debug info di **Windows**
- Runtime trace sink (beyond `--trace` / `int3` stub)

### Optimization (20)

- `--register-mode assisted`
- Global dead-code elimination
- Propagation / optimization lanjutan

### CPU & SIMD (21)

- Typed `f64x4` vectors
- `cmpxchg` atomics penuh
- Windows ymm callee-save / restore

### Debugger & Lab (24)

- Full MI **memory** view di DAP
- Parity MCP tools Lab UI ↔ CLI (semua perintah)

### Language / memory (lintas-fase, lihat `docs/memory-model.md`)

- `ptr<T>`, `slice<T>`, checked indexing
- **Runtime heap helpers (shipped):** `hlax_malloc`, `hlax_realloc`, `hlax_free` — [`dynamic-array-heap.hla64`](../examples/curriculum/05-memory/dynamic-array-heap.hla64); static-backed [`dynamic-array.hla64`](../examples/curriculum/05-memory/dynamic-array.hla64) for fixed-capacity vectors
- **Project Euler suite:** [`examples/project-euler/`](../examples/project-euler/) — #1–#25 (+ stubs #26–#50), argv runner with `ParseU64`, curriculum tests for #1/#4/#10
- Macro system, LLVM backend, self-hosting (P3)

---

## 7. Risiko

- **Risiko 6 (termitigasi):** pertumbuhan fitur sebelum fondasi stabil → Fase 9.5 + native tests selesai.

Lihat Plan Section 11 untuk risiko lengkap (lokal).
