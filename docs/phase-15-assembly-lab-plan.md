# Phase 15 — AI Assembly Lab (Rencana Eksekusi)

> **Status:** Rencana matang · belum diimplementasi  
> **Prasyarat:** Phase 14 (LSP hardened), Phase 19–24 (debug, proof bundle, DAP Linux), Phase 18 (verifiers)  
> **Versi dokumen:** 1.0 · 2026-06-07

---

## 1. Visi dan batas scope

### 1.1 Apa yang Phase 15 **bukan**

- Bukan mengganti VS Code/Cursor sebagai editor utama.
- Bukan IDE generik seperti Visual Studio (refactor penuh, designer UI, plugin marketplace).
- Bukan web playground saja (itu sudah ada di `docs/playground/`).

### 1.2 Apa yang Phase 15 **adalah**

**AI Assembly Lab** — aplikasi desktop **cross-platform native** (.NET) yang memvisualisasikan pipeline HlaX64 end-to-end untuk manusia dan agent:

```text
source → diagnostics → IR → ABI → NASM → binary → run → trace/debug → proof bundle
```

Positioning (selaras audit):

```text
HlaX64 is an explainable, verifiable native-code layer for humans and AI agents.
```

Lab menjadi **shell visual** untuk fitur yang sudah ada di CLI/MCP/LSP/DAP, bukan duplikasi compiler.

### 1.3 Keputusan platform (matang)

| Opsi | Cross-platform | Native feel | Reuse .NET toolchain | Verdict |
|------|----------------|------------|----------------------|---------|
| **Avalonia UI 11** | Win / Linux / macOS | Ya (Skia) | ✅ penuh | **Pilihan utama** |
| WPF | Windows only | Ya | ✅ | Tidak — melanggar syarat cross-platform |
| MAUI | Win / Mac / mobile | Campuran | ✅ | Risiko mobile scope creep |
| Tauri / Electron | Ya | WebView | ❌ bridge | Berat; tidak perlu untuk lab teknis |
| VS Code extension only | Ya | Editor-bound | ✅ | Sudah ada — bukan Phase 15 |

**Keputusan:** `HlaX64.AssemblyLab` — proyek **Avalonia 11** + **.NET 10**, target framework `net10.0`, RID `win-x64`, `linux-x64`, `osx-x64` (x64 only, selaras branding HlaX64).

**Alasan Avalonia:** satu codebase, native packaging, MVVM cocok untuk panel dock (source | IR | NASM | registers), CI headless testable via `Avalonia.Headless`.

---

## 2. Arsitektur Lab

### 2.1 Prinsip

1. **Thin UI, fat toolchain** — UI memanggil `HlaX64.Compiler`, CLI services, MCP JSON contracts; tidak fork parser.
2. **Satu backend service** — `AssemblyLabBackend` wrap `Compilation`, `ExplainReport`, `StackVerifier`, `AbiVerifier`, proof bundle.
3. **Agent-first** — setiap aksi UI punya equivalent MCP/CLI (`plan`, `diff`, `explain`, `build --proof-bundle`).
4. **Offline-first** — tidak wajib cloud; agent lokal via MCP stdio.

### 2.2 Diagram

```text
┌─────────────────────────────────────────────────────────────┐
│  HlaX64.AssemblyLab (Avalonia)                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────┐  │
│  │ Source   │ │ IR/NASM  │ │ Registers│ │ Proof/Agent  │  │
│  │ editor   │ │ panes    │ │ / Stack  │ │ panel        │  │
│  └────┬─────┘ └────┬─────┘ └────┬─────┘ └──────┬───────┘  │
│       └────────────┴────────────┴───────────────┘          │
│                         │                                     │
│              AssemblyLabBackend (in-proc)                   │
└─────────────────────────┼───────────────────────────────────┘
                          │
     ┌────────────────────┼────────────────────┐
     ▼                    ▼                    ▼
 HlaX64.Compiler    HlaX64.DebugAdapter   HlaX64.McpServer
 (Compilation)       (DAP → gdb)          (optional embed)
```

### 2.3 Integrasi fitur existing

| Fitur repo | Peran di Lab |
|------------|--------------|
| LSP (`LanguageServerEditorServices`) | Reuse logic via shared lib atau AvaloniaEdit + LSP client optional |
| DAP (`HlaX64.DebugAdapter`) | Debug session panel (Linux gdb dulu) |
| `.hlamap.json` | Sync scroll source ↔ NASM |
| `--proof-bundle` | Tab “Audit” + export ZIP |
| `hla64 plan` / `diff` | Agent approval dialog sebelum run |
| MCP server | Tombol “Connect agent” → spawn `HlaX64.McpServer` |

---

## 3. UX utama (MVP Lab)

### 3.1 Layout (dock)

| Panel | Konten |
|-------|--------|
| **Kiri** | Tree project (`hla64.toml`), file `.hla64`, diagnostics list |
| **Tengah** | Source editor (syntax highlight reuse TextMate grammar atau AvaloniaEdit) |
| **Kanan atas** | Tab: IR · NASM · Disasm · ABI report |
| **Kanan bawah** | Output (build/run), trace log, capabilities |
| **Bawah** | Status: target triple, CPU features, optimization level |

### 3.2 Alur pengguna

1. Open folder / `hla64 new console`
2. Edit → live diagnostics (debounced compile)
3. **Build** → NASM pane + optional proof bundle
4. **Run** → exit code + stdout; **Run traced** jika `--trace`
5. **Debug** → launch DAP, breakpoints di gutter source map
6. **Explain** → highlight IR lines correlated via `.hlamap.json`
7. **Agent** → MCP tool `explain` + `suggestedFix` → apply patch

### 3.3 AI Assembly Lab (differensiator)

| Fitur | Deskripsi |
|-------|-----------|
| **Semantic diff view** | Visual `hla64 diff` (procedure, ABI, clobbers) bukan text diff saja |
| **Approval gate** | `hla64 plan --json` ditampilkan sebelum run/build |
| **Repair loop** | Panel diagnostic → suggested fix → preview → apply |
| **Proof bundle viewer** | Browse `capabilities.json`, `abi.json`, tests |

---

## 4. Rencana sprint (Phase 15)

Estimasi **6 sprint** (~6–10 minggu part-time). Setiap sprint: UI + backend + test + docs.

### Sprint 15.1 — Foundation

- [ ] `src/HlaX64.AssemblyLab/` Avalonia project + MVVM skeleton
- [ ] `AssemblyLabBackend.Compile()`, `GetDiagnostics()`
- [ ] Open file / open folder (`hla64.toml` detection)
- [ ] CI: `dotnet build` Lab on win + linux (headless)
- [ ] **Done when:** open `examples/00-getting-started/hello.hla64`, see diagnostics

### Sprint 15.2 — Pipeline panes

- [ ] Tab IR, NASM, ABI (reuse `ExplainReport`, `verify-abi` JSON)
- [ ] Build / Run buttons (call CLI or in-proc `Compilation`)
- [ ] Target selector: linux-x64-sysv / windows-x64-msabi
- [ ] **Done when:** build hello, NASM pane matches `emit-nasm`

### Sprint 15.3 — Source map sync

- [ ] Load `.hlamap.json`; click source line → jump NASM
- [ ] Column-aware highlight (extend map if needed)
- [ ] Disasm pane (`hla64 disasm` output)
- [ ] **Done when:** sync works on `stack-args-sysv.hla64`

### Sprint 15.4 — Debug integration

- [ ] Embed/spawn `HlaX64.DebugAdapter`; breakpoint gutter
- [ ] Register + stack view (read gdb MI output)
- [ ] Linux-first; Windows → lldb follow-up
- [ ] **Done when:** breakpoint on `mov` stops in Lab (Linux + gdb)

### Sprint 15.5 — Proof & agent

- [ ] Proof bundle export UI
- [ ] Plan / diff / capabilities panels
- [ ] MCP client: list tools, run `explain`, show `suggestedFix`
- [ ] **Done when:** agent repair loop demo on intentional HLAX error

### Sprint 15.6 — Polish & ship

- [ ] Packaging: `dotnet publish` per RID; optional MSIX/deb
- [ ] Settings: toolchain path, WSL, default target
- [ ] Tutorial: `docs/tutorials/06-assembly-lab.md`
- [ ] **Done when:** release asset `AssemblyLab-*` di GitHub Releases

---

## 5. Stack teknis

| Layer | Pilihan |
|-------|---------|
| UI | Avalonia 11, Fluent theme, Dock.Avalonia atau panel grid |
| Editor | AvaloniaEdit + `.hla64` grammar (port from VS Code tmLanguage) |
| MVVM | CommunityToolkit.Mvvm |
| Backend | Referensi langsung ke `HlaX64.Compiler`, `HlaX64.Cli` services |
| Debug | Process spawn `hla64 debug --stdio` + JSON-RPC client |
| Test | `Avalonia.Headless.XUnit` untuk VM; integration manual checklist |

### 5.1 Struktur proyek (target)

```text
src/HlaX64.AssemblyLab/
  App.axaml
  ViewModels/   MainWindowViewModel, EditorViewModel, DebugViewModel
  Views/        MainWindow, EditorPane, PipelinePane, AgentPane
  Services/     AssemblyLabBackend, McpClientHost, DebugSessionHost
  Models/       CompileResult, ProofBundleManifest
tests/HlaX64.AssemblyLab.Tests/
```

---

## 6. Cross-platform & packaging

| Platform | Build | Run native binary | Catatan |
|----------|-------|-------------------|---------|
| **Windows** | `dotnet publish -r win-x64` | Ya | NASM + lld-link; path via `doctor` |
| **Linux** | `dotnet publish -r linux-x64` | Ya | gcc + nasm |
| **macOS x64** | `dotnet publish -r osx-x64` | Ya (best effort) | toolchain user-installed |

**WSL bridge (Windows):** toggle “Run via WSL” untuk `linux-x64-sysv` — reuse logic dari CLI `doctor`.

**Tidak termasuk Phase 15:** ARM64 Mac primary, mobile, store submission.

---

## 7. Keamanan & agent policy

- Default **sandbox policy** dari `capabilities.json`: warn sebelum run jika syscalls > {write, exit}.
- Agent actions log ke `lab-session.json` (audit trail).
- Tidak auto-run binary tanpa user confirm jika capabilities berubah (semantic diff).

---

## 8. Acceptance criteria (Phase 15 selesai)

- [ ] Avalonia app runs on **Windows 11** and **Ubuntu 22.04+**
- [ ] Open project, edit, build, run, view IR/NASM/ABI without CLI manual steps
- [ ] Source map sync + disasm pane functional
- [ ] Debug session on Linux with ≥1 breakpoint
- [ ] Proof bundle export + viewer
- [ ] MCP explain/repair demo documented
- [ ] CI builds Lab; release workflow optional artifact
- [ ] RFC `0024-assembly-lab.md` + tutorial + roadmap Phase 15 ✅

---

## 9. Risiko dan mitigasi

| Risiko | Dampak | Mitigasi |
|--------|--------|----------|
| Avalonia + Skia di Linux Wayland | Rendering glitches | Test CI on ubuntu; X11/Wayland note in docs |
| gdb MI complexity | Debug sprint slip | Sprint 15.4 scope: CLI gdb wrapper dulu, MI later |
| Duplikasi LSP | Maintenance burden | Shared `LanguageServerEditorServices` library extract |
| Scope creep (full IDE) | Never ship | Strict panel list §3.1; defer refactor/rename |
| Windows debug | No gdb | Sprint 15.4 Linux only; 15.6 doc lldb Phase 15+ |

---

## 10. Relasi Phase 24

Phase 24 (Debugger & Assembly Lab) **overlap** dengan Phase 15. Keputusan:

- **Phase 15** = deliver **desktop Lab shell** (Avalonia).
- **Phase 24 remaining** setelah 15 = DAP parity Windows, memory view advanced, AI repair production-hardening.

Setelah Phase 15 ship, ubah roadmap:

```text
Phase 15 ✅ Done (Assembly Lab desktop)
Phase 24 → “DAP & debug hardening” (tanpa GUI baru)
```

---

## 11. Langkah segera (pre-Sprint 15.1)

1. Spike 2 hari: Avalonia + AvaloniaEdit + load `hello.hla64` + one diagnostic.
2. Extract shared `HlaX64.EditorServices` dari LSP (optional refactor).
3. RFC 0024 draft dari dokumen ini.
4. Issue backlog: label `phase-15`, 6 milestones = sprint di atas.

---

## 12. Referensi

- [roadmap.md](roadmap.md) — Phase 15 row  
- [compiler-architecture.md](compiler-architecture.md) — tooling integration  
- [rfcs/0022-proof-bundle.md](../rfcs/0022-proof-bundle.md)  
- [rfcs/0023-dap-mvp.md](../rfcs/0023-dap-mvp.md)  
- Audit §5.65–5.69, Phase 24 proposal  
