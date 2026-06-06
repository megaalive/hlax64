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
| **9.5** | **Compiler Architecture Stabilization** | 🛠 **Active** | CompilationModel ✅, TypeSystem ✅, IR ✅, ABI lowerer ✅, native tests ✅, docs sync 🔜 |
| 10 | Benchmark runner | ✅ Done | `hla64 bench` |
| 11 | Windows x64 backend | ⏳ Pending | `windows-x64-msabi` target |
| 12 | C ABI & C# interop | ⏳ Pending | export, P/Invoke generator |
| 13 | MCP server | ⏳ Pending | tools untuk AI agent |
| 14 | LSP & editor tooling | ⏳ Pending | diagnostics, hover, completion |
| 15 | AI Assembly Lab / GUI | ⏳ Pending | eksperimen UI (Avalonia/WPF) |

---

## 2. Tier eksekusi saat ini

### Tier 1 — Dokumentasi (zero risk)

- [x] `docs/roadmap.md` (file ini)
- [ ] `docs/compiler-architecture.md` — diagram pipeline IR + 7 workstream
- [ ] `docs/runtime-contract.md` — format clobber metadata per fungsi runtime
- [ ] `docs/examples.md` — katalog & cara menjalankan
- [ ] `README.md` — sync badge "Fase 0–9 Done · 9.5 Active"

### Tier 2 — Sample tests (memenuhi target MVP "min 10 samples")

✅ **Sudah 10 sample** — semua PASS:

- `hello`, `exitcode`, `add_two`, `count`, `simple`, `local_var`, `if_else`, `procedure_1arg`, `procedure_6args`, `comparison_signed`

### Tier 3 — Mini CLI command (mitigasi Risiko 2)

- [x] `hla64 explain-abi --target linux-x64-sysv` ✅

---

## 3. Setelah 9.5 selesai

Urutan roadmap yang direkomendasikan (lihat Plan Section 9):

```
Fase 9.5  →  Fase 10 (Bench)  →  Fase 11 (Windows)  →  Fase 12 (C# interop)
Fase 13 (MCP)  →  Fase 14 (LSP)  →  Fase 15 (GUI)
```

> **Aturan keras**: jangan sentuh Fase 10–15 sebelum semua item
> Definition of Done Fase 9.5 (15 checklist) terpenuhi.

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

- **Risiko 6 (utama saat ini)**: pertumbuhan fitur sebelum fondasi stabil.
  → Mitigasi: **Fase 9.5 active; tidak menambah syntax baru** sampai DoD terpenuhi.
