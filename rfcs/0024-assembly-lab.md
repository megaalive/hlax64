# RFC 0024 — AI Assembly Lab (Phase 15)

| Field | Value |
|-------|-------|
| **Status** | Accepted |
| **Phase** | 15 |
| **Author** | megaalive |

## Summary

Phase 15 delivers **HlaX64.AssemblyLab** — a cross-platform Avalonia 11 desktop application that visualizes the HlaX64 compile pipeline (source → diagnostics → IR → ABI → NASM → build/run → debug → proof bundle) without duplicating compiler logic. The UI is a thin shell over `HlaX64.Compiler`, `ExplainReport`, CLI toolchain helpers, and `HlaX64.DebugAdapter`.

## Motivation

The CLI, LSP, MCP, and DAP already expose the toolchain for humans and agents. A dedicated **Assembly Lab** makes the end-to-end pipeline explorable in one window — especially for learning, debugging ABI/lowering, and agent repair loops — while remaining offline-first and aligned with the project positioning:

> HlaX64 is an explainable, verifiable native-code layer for humans and AI agents.

## Detailed design

### Project layout

```text
src/HlaX64.AssemblyLab/
  Services/AssemblyLabBackend.cs   — Compile, Build, Run, proof bundle, source map
  Services/DebugSessionHost.cs     — DAP MVP panel (in-process / spawn CLI)
  ViewModels/MainWindowViewModel.cs
  Views/MainWindow.axaml
tests/HlaX64.AssemblyLab.Tests/  — backend unit tests
```

### Backend (`AssemblyLabBackend`)

| Method | Behavior |
|--------|----------|
| `Compile(sourcePath, sourceText, target)` | Debounced live compile; returns diagnostics, IR/NASM/ABI text, optional `.hlamap.json` |
| `Build(...)` | Full NASM emit + assemble + link; writes `.hlamap.json` when `--source-map` equivalent enabled |
| `Run(...)` | Build then spawn binary (WSL bridge on Windows for Linux ELF) |
| `ExportProofBundle(...)` | Reuses `ProofBundleWriter` |
| `AnalyzeCapabilities(...)` | Reuses `CapabilityAnalyzer` |

Targets: `linux-x64-sysv`, `windows-x64-msabi`.

### UI (MVP)

| Panel | Content |
|-------|---------|
| Diagnostics list | Structured HLAX codes (line, message) |
| Tabs | Source · IR · NASM · ABI |
| Output / DAP / Capabilities | Build/run log, DAP message trace, `capabilities.json` summary |
| Toolbar | Open file/folder, Build, Run, Debug, Proof bundle, target selector |

Source map sync: double-click diagnostic or navigate by line → highlight corresponding NASM line (basic line match via `SourceMapDocument.LookupBySource`).

Debug: **Linux-first** — DAP MVP via `DebugAdapterHost` capabilities display; full gdb session deferred to Phase 24 hardening.

## Alternatives considered

| Option | Verdict |
|--------|---------|
| WPF | Rejected — Windows-only |
| VS Code extension only | Already exists (Phase 14); not a replacement |
| Electron/Tauri | Rejected — unnecessary bridge weight |
| Duplicate parser in UI | Rejected — thin UI, fat toolchain |

## Compatibility

Additive. No language or CLI breaking changes. New solution projects only.

## Educational / AI impact

- Learners see IR/ABI/NASM side-by-side while editing.
- Agents can mirror Lab actions via existing MCP/CLI (`explain`, `build --proof-bundle`, `plan`).
- Proof bundle export supports audit/review workflows.

## Implementation plan

| Sprint | Deliverable |
|--------|-------------|
| 15.1 | Avalonia shell, live compile, backend tests, CI build |
| 15.2 | IR/NASM/ABI tabs, Build/Run, target selector |
| 15.3 | Source map load + NASM highlight |
| 15.4 | Debug button + DAP output panel (MVP) |
| 15.5 | Proof bundle export + capabilities panel |
| 15.6 | RFC, tutorial, roadmap, CHANGELOG |

## Deferred

- AvaloniaEdit syntax highlighting / gutter breakpoints
- Windows DAP (lldb)
- Embedded MCP client (`explain` repair loop in UI)
- `dotnet publish` release artifacts per RID
- Disasm pane (`hla64 disasm`)
- Semantic diff / plan approval gate UI

## Unresolved questions

- Extract shared `HlaX64.EditorServices` from LSP for hover/completion in Lab editor?
- WSL toggle in settings vs auto-detect only?
