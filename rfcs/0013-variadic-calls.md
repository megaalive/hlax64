# RFC 0013 — Variadic calls (Phase 17 Sprint 5)

**Status:** RFC only — not implemented  
**Since:** 0.1.0-alpha

## Summary

Variadic C functions (`printf`, etc.) require different calling conventions on SysV vs Windows x64.

## SysV AMD64 variadic rules (target)

- Integer args: `rdi`, `rsi`, `rdx`, `rcx`, `r8`, `r9`
- SSE args: `xmm0`–`xmm7`
- `AL` = number of SSE registers used
- Stack overflow for both classes
- `va_list` layout per psABI

## Windows x64 variadic rules

- First 4 integer/pointer in `rcx`, `rdx`, `r8`, `r9`
- First 4 float in `xmm0`–`xmm3`
- All variadic args also copied to stack shadow area
- Different `va_start` layout

## MVP (Phase 17 Sprint 5)

- Parse `extern variadic procedure printf(...)` for forward compatibility
- Emit **HLAX0055** — variadic not yet supported
- Full `printf` lowering deferred to Phase 18

## Struct by-value (MVP)

Record parameters at procedure boundaries are passed as **hidden pointers** (8-byte address in integer register). Callers pass `&recordVar`. Fields accessed via pointer indirection in the callee.

Full ≤16-byte register struct passing (SysV classification) deferred to Phase 18.
