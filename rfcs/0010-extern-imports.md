# RFC 0010 — Extern procedures and import libraries (Phase 17 Sprint 2)

**Status:** Implemented (Phase 17 Sprint 2)  
**Since:** 0.1.0-alpha

## Summary

Declare C/ABI symbols without a body and link against system or third-party libraries.

```hla
extern procedure puts(msg: cstring): int32 from "libc.so";
extern procedure GetTickCount(): uint32 from "kernel32.dll";
```

## Syntax

- Program scope, before `begin`
- `extern procedure Name(params): ReturnType;`
- Optional `from "library"` — maps to `-lc` / `kernel32.lib` at link time
- Calls emit `extern Name` in NASM and `call Name`

## Link hints

| `from` clause | Linux link flag | Windows link flag |
|---------------|-----------------|-------------------|
| `"libc.so"` / `"libc"` | `-lc` | — |
| `"kernel32.dll"` | — | `kernel32.lib` |

## Diagnostics (HLAX0050+)

| Code | Meaning |
|------|---------|
| HLAX0050 | Duplicate or conflicting extern |
| HLAX0052 | Record type in extern param (use `ptr`) |
| HLAX0053 | Unknown signature type |
| HLAX0054 | Unknown call target |
| HLAX0055 | Variadic extern not supported (RFC 0013) |

## Example

`examples/interop/07-interop/extern-puts.hla64`
