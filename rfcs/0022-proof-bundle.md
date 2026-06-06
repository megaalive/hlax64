# RFC 0022 — Proof Bundle (Phase 23)

| Field | Value |
|-------|-------|
| **Status** | MVP |
| **Phase** | 23 |

## Command

```bash
hla64 build app.hla64 --proof-bundle --source-map
```

## Output directory

`proof-bundle/` containing: binary, `.nasm`, `ir.json`, `.hlamap.json`, `abi.json`, `capabilities.json`, `build.json`.

## Capabilities

Static syscall/extern analysis — `filesystemAccess: false` by default.
