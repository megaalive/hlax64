# RFC 0011 — Function pointers (Phase 17 Sprint 3)

**Status:** Implemented (Phase 17 Sprint 3)  
**Since:** 0.1.0-alpha

## Summary

Procedure signature type aliases and indirect calls through variables/parameters holding code addresses.

```hla
type CompareFn := procedure(a: ptr; b: ptr): int32;

procedure Sort(data: ptr; count: uint64; compare: CompareFn);
...
call compare(a, b);
```

## Model

- `type Name := procedure(...): RetType;` defines a 64-bit function pointer type (alias for `ptr` with signature checking at call sites)
- Locals and params of alias type stored as 8-byte addresses
- `call fn(...)` when `fn` is a local/param lowers to `mov rax, [slot]` / `call rax` after SysV/Win64 argument setup

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0051 | Type alias conflict or duplicate |

## Example

`examples/07-interop/indirect-call.hla64`
