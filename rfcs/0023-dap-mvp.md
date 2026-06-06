# RFC 0023 — DAP and Virtual Docs (Phase 24)

| Field | Value |
|-------|-------|
| **Status** | MVP stub |
| **Phase** | 24 |

## DAP

`hla64 debug --stdio` — minimal JSON request/response (`launch`, `disconnect`).

Full Debug Adapter Protocol server deferred.

## LSP virtual documents

Commands: `hla64.showIr`, `hla64.showNasm`, `hla64.showStackLayout` via `workspace/executeCommand`.

## MCP repair contract

`explain` tool returns diagnostics with `span` and `suggestedFix` template JSON.
