# Web Playground Design (Draft)

> **Status:** Phase 1 (static site) in progress — see [roadmap.md](roadmap.md) §5.
> Phase 2 (live explain API) not yet deployed.

## Goals

- Browser editor for `.hla64` with syntax highlighting (reuse `editors/vscode` grammar)
- Live **diagnostics**, **IR**, and **NASM** panes (no native execution in v1)
- Shareable snippets via URL hash or gist

## Non-goals (v1)

- Running native code in the browser
- Full LSP parity

## Architecture (proposed)

```text
Browser (Monaco + TextMate grammar)
  → POST /api/compile  (WASM or remote HlaX64 CLI)
  → JSON { diagnostics, ir, nasm }
```

## API sketch

```json
POST /api/explain
{
  "source": "program hello; ...",
  "target": "linux-x64-sysv"
}
```

Response matches `hla64 explain --json` (`schemaVersion`, `ir`, `lowered`, `nasm`).

## Implementation phases

1. Static site + embedded examples (GitHub Pages)
2. Server-side `hla64 explain --json` endpoint (rate-limited)
3. Optional WASM port of compiler front-end (long term)

## Related

- [mcp-tools.md](mcp-tools.md) — agent integration today
- [development.md](development.md) — local CLI workflow
