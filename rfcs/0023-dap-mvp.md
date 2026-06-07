# RFC 0023 — DAP and Virtual Docs (Phase 24)

| Field | Value |
|-------|-------|
| **Status** | Partially implemented |
| **Phase** | 24 |

## DAP (`HlaX64.DebugAdapter`)

`hla64 debug --stdio` — Debug Adapter Protocol over stdin/stdout.

Capabilities: `initialize`, `launch`, `configurationDone`, `setBreakpoints`, `threads`, `stackTrace`, `scopes`, `continue`, `disconnect`.

Backend: Linux gdb (MI/CLI wrapper). **Windows:** follow-up (lldb or defer).

VS Code: see `editors/vscode/launch.json`.

## LSP virtual documents

Commands: `hla64.showIr`, `hla64.showNasm`, `hla64.showStackLayout` via `workspace/executeCommand`.

## Deferred

- Variables/evaluate, conditional breakpoints, Windows lldb backend
