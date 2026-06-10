# HlaX64 — Roadmap

> **Document status**: Active · aligned with `HlaX64_Project_Plan.md` (local consolidation)
> **Language**: `HlaX64` v0.1 (Draft Language Reference)
> **Active target**: `linux-x64-sysv`

This document is a quick-read execution summary. For full detail (rationale,
deliverables, acceptance criteria), see [`HlaX64_Project_Plan.md`](../HlaX64_Project_Plan.md)
when available in your local clone.

### Status legend

| Label | Meaning |
|-------|---------|
| **✅ Done** | Phase scope complete; shipped in toolchain and examples/tests. |
| **✅ Hardened** | Phase MVP shipped; followed by hardening (lookup, stubs, CI, docs). |
| **🔜 Deferred** | Not started yet; see [§6 Open backlog](#6-open-backlog-deferred). |

> **Note:** *MVP* in older RFCs/commits means *minimum scope that was promised*,
> not “still missing”. If a sprint is marked **(done)**, the feature exists even
> if it is not the “final version”.

---

## 1. Phase map

| Phase | Name | Status | Summary |
|------|------|--------|-----------|
| 0 | Foundation & repo setup | ✅ Done | repo, solution, CLI `--version` |
| 1 | Lexer & Parser | ✅ Done | program, procedure, call |
| 2 | NASM Backend | ✅ Done | valid NASM x64, operand order |
| 3 | Toolchain build Linux x64 | ✅ Done | `hla64 build`, `hla64 run` |
| 4 | Runtime `stdout.put` | ✅ Done | syscall / library write |
| 5 | Semantic Analyzer | ✅ Done | registers, instructions, scope |
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

**All phases 0–24 are at least Done/Hardened.** Further work is **Deferred** items only (§6).

---

## 2. Sprint notes (history — all shipped)

### Phase 15 — Assembly Lab

- **15.1–15.6 (done):** Avalonia shell, IR/NASM/ABI tabs, build/run, source map, DAP panel, proof bundle, RFC + tutorial
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

## 3. Current snapshot

| Area | Status |
|------|--------|
| Compiler phases 0–24 | ✅ Done / Hardened (see §1) |
| Curriculum examples | 58 manifests in `tests/examples-curriculum/` + real-tools / bug-farm / invalid |
| Automated tests | 400+ (`dotnet test` — compiler + AssemblyLab) |
| Build targets | Linux SysV + Windows MS ABI |
| Product docs | `docs/language-spec.md`, tutorials, RFC 0001–0024 |

**Next focus:** **v0.2.0-alpha** (§5) — onboarding & playground, not new language features.

---

## 5. v0.2.0-alpha — Useful Assembly Tools & Onboarding

> **Release theme:** Playground as the main GitHub Pages product — open link →
> try an example → see NASM → understand the value → then clone the repo.

| Item | Status | Notes |
|------|--------|---------|
| Playground example picker (181 programs, nested by `examples/` category) | ✅ Done | `docs/playground/manifest.json`, `?example=wc` |
| Generated NASM pane (cached `explain --json`) | ✅ Done | `docs/playground/cache/`, `scripts/generate-playground-{manifest,cache}.ps1` |
| Explain-this-line (heuristic tutor) | ✅ Done | Line click + explanation tab |
| Copy AI Debug / Explain / Optimize prompt | ✅ Done | Playground AI tab |
| Live explain API (edit source → fresh NASM) | 🔜 Planned | `playground-design.md` Phase 2 |
| Monaco + syntax highlighting | 🔜 Planned | Reuse `hla64.tmLanguage.json` |
| Gallery deep links from README | ✅ Done | `/playground?example=hexdump`, etc. |
| GitHub Pages as “product” (Home / Playground / Course / …) | ✅ Done | `docs/index.html` nav |
| `cat`, `strings`, `printf` curriculum examples | 🔜 Planned | `wc`/`hexdump`/`filemagic` already exist |
| Tutorial argv + file I/O path | 🔜 Planned | Extend tutorials / course doc |
| Assembly Lab parity in browser (build/run) | 🔜 Deferred | Desktop Lab remains full surface |

**Already shipped (do not duplicate):** Assembly Lab (live IR/NASM, Explain, Agent, DAP), MCP
`explain`, 58 curriculum manifests, real-tools Windows + Linux ports.

**Non-goals v0.2:** macro, LLVM, `ptr<T>`, per-file `#pragma target` — remain in §6.

---

## 4. Execution tiers (historical)

These tiers record early project milestones; all are ✅.

### Tier 1 — Documentation

- [x] `docs/roadmap.md`, `docs/compiler-architecture.md`, `docs/runtime-contract.md`, `docs/examples.md`
- [x] `README.md` — phases 0–24 Done/Hardened (not only 0–13)

### Tier 2 — Samples & curriculum

- [x] Native integration tests (`tests/samples/`, `tests/examples-curriculum/`)
- [x] Real-tools, bug-farm, invalid mirrors, Linux ports

### Tier 3 — CLI utilities

- [x] `hla64 explain-abi`, `explain`, `verify-*`, `bench`, MCP tools

---

## 5. Feature priorities (summary)

| Tier | Scope | Status |
|------|-------|--------|
| **P0** | parser, emitter, CLI, test runner, phase 9.5 stabilization | ✅ |
| **P1** | control flow, types, IR/ABI, Windows, C# interop | ✅ |
| **P2** | MCP, LSP, benchmark, Assembly Lab | ✅ |
| **P3 / Deferred** | macro, LLVM, OSDev, full HLA compat, JIT, self-hosting, §6 | 🔜 |

Legacy priority detail: Plan Section 10 (local).

---

## 6. Open backlog (Deferred)

The only work **not** on the active roadmap:

### Debug & explainability (19)

- Full DWARF debug info on **Windows**
- Runtime trace sink (beyond `--trace` / `int3` stub)

### Optimization (20)

- `--register-mode assisted`
- Global dead-code elimination
- Further propagation / optimization

### CPU & SIMD (21)

- Typed `f64x4` vectors
- Full `cmpxchg` atomics
- Windows ymm callee-save / restore

### Debugger & Lab (24)

- Full MI **memory** view in DAP
- Parity MCP tools Lab UI ↔ CLI (all commands)

### Language / memory (cross-phase, see `docs/memory-model.md`)

- `ptr<T>`, `slice<T>`, checked indexing
- **Runtime heap helpers (shipped):** `hlax_malloc`, `hlax_realloc`, `hlax_free` — [`dynamic-array-heap.hla64`](../examples/curriculum/05-memory/dynamic-array-heap.hla64); static-backed [`dynamic-array.hla64`](../examples/curriculum/05-memory/dynamic-array.hla64) for fixed-capacity vectors
- **Project Euler suite:** [`examples/project-euler/`](../examples/project-euler/) — #1–#25 (+ stubs #26–#50), argv runner with `ParseU64`, curriculum tests for #1/#4/#10
- Macro system, LLVM backend, self-hosting (P3)

---

## 7. CPU outlook & multi-architecture (informative)

**Not an implementation priority** — design guidance so HlaX64 does not lock
everything to NASM/x86 as AArch64 grows in importance.

### Market outlook (realistic)

| Architecture | Expected role through the late 2030s |
|--------------|-------------------------------------|
| **AArch64** | First-class: mobile, Mac, embedded, much of cloud |
| **x86-64** | Still strong: Windows desktop, gaming, workstations, industrial systems, legacy software/drivers |

Both coexist. x86 binary compatibility is a burden and a strength — many organizations still run stacks that are decades old.

### HlaX64 position

- **Primary focus stays x86-64** — Windows + Linux, NASM, Assembly Lab, `linux-x64` / `windows-x64` runtime trees.
- **Keep the product name** — do not rebrand to a generic “Hla64” compiler; the x86 teaching identity is intentional.
- **Compiler architecture already separates layers:** source → IR → ABI lowerer → backend → link. An ARM backend should be a sibling project (e.g. HlaArm64) or a plug-in backend pack, not a repo pivot.

### Future targets (not scheduled)

1. **Now:** Linux x64, Windows x64 (✅)
2. **Optional:** macOS x64 (legacy)
3. **Later:** Linux AArch64, macOS AArch64, Windows ARM64

An ARM backend **cannot** use NASM. Reasonable options later: GNU/Clang assembly text, LLVM IR, or object emission via LLVM MC. For assembly lab teaching, **emit `.s` + clang/gcc link** best matches today’s philosophy.

### Design steps without writing an ARM backend

- Document `LoweredFunction` + [runtime-contract.md](runtime-contract.md) as the cross-backend contract.
- Reduce x86 register assumptions above the ABI lowerer where possible.
- New runtime APIs: specify `hlax_*` first, implement per ISA in separate runtime trees.

See also: [architecture.md](architecture.md) § Multi-architecture strategy, [compiler-architecture.md](compiler-architecture.md) § Future backends.

---

## 8. Risks

- **Risk 6 (mitigated):** feature growth before a stable foundation → Phase 9.5 + native tests completed.

Full risk list: Plan Section 11 (local).
