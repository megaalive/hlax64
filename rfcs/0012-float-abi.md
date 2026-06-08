# RFC 0012 — Floating-point ABI (Phase 17 Sprint 4)

**Status:** Partial (Phase 17 Sprint 4 MVP)  
**Since:** 0.1.0-alpha

## Summary

MVP parameter passing and return for `float32` / `float64` (aliases `real32` / `real64`).

## Register assignment (MVP)

Arguments classified in source order:

| Class | SysV | Windows |
|-------|------|---------|
| Integer/pointer | `rdi`…`r9` (6) | `rcx`, `rdx`, `r8`, `r9` |
| float32/float64 | `xmm0`…`xmm7` | `xmm0`…`xmm3` |

- Callee prologue homes float params to stack via `movss`/`movsd` from assigned XMM register
- `@returns("xmm0")` documents float return (no automatic float expressions yet)

## Deferred (Phase 18+)

- Runtime `:=` float expressions
- Full homing / red-zone rules for mixed stack overflow
- SSE arithmetic intrinsics or operators

## Example

`examples/curriculum/06-abi/float-return.hla64`
