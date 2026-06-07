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

Debug: DAP via `hla64 debug --stdio` — `IDebugBackend` with **gdb** (Linux) and **lldb** (Windows, PATH or `Program Files/LLVM/bin`). Lab **Debug** builds first, spawns CLI, sends initialize/launch/setBreakpoints/configurationDone.

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

## Batch 3 (done)

- **DAP:** `IDebugBackend`, `GdbBackend`, `LldbBackend`; `DebugSessionHost` JSON-RPC with seq; Debug command builds then spawns `hla64 debug --stdio`
- **Agent:** In-proc `ExplainAgentService` (MCP-style `suggestedFix`); Agent tab + Explain button
- **Plan / diff:** `PlanService`, `SemanticDiffService`; Plan and Diff tabs; **Plan approved** checkbox gates Build/Run/Proof bundle until user confirms (refreshes on source/target change)

## Batch 4 (done)

- **Apply fix:** `SuggestFixApplier` + **Apply Fix** toolbar button; `suggestedFix.replacement` from agent explain JSON
- **DAP MI:** `MiOutputParser`, `DebugEngineSession`; gdb/lldb stopped events, stack frames, register scope via DAP `variables`
- **MCP Lab client:** `McpSessionHost` spawns `HlaX64.McpServer`; MCP tab with Start / Tools / Explain

## Deferred

- Full MI memory view in DAP panel
- MCP tool parity with full CLI surface from Lab UI

## Unresolved questions

- Extract shared `HlaX64.EditorServices` from LSP for hover/completion in Lab editor?
- WSL toggle in settings vs auto-detect only?
