# RFC 0013 — Variadic calls (Phase 17 Sprint 5)

**Status:** Partially implemented (SysV integer + cstring MVP)  
**Since:** 0.1.0-alpha

## Summary

Variadic C functions (`printf`, etc.) require different calling conventions on SysV vs Windows x64.

## SysV AMD64 variadic rules (implemented MVP)

- Integer args: `rdi`, `rsi`, `rdx`, `rcx`, `r8`, `r9`
- `AL` = number of SSE registers used (0 for int-only MVP)
- Stack overflow for extra integer args
- Integer + `cstring` variadic args only

## Windows x64 variadic rules

- Deferred — emit HLAX0055 for variadic float args; full MS ABI variadic deferred

## MVP

- Parse and register `extern variadic procedure printf(...)`
- Lower SysV calls with `mov al, 0` for int-only variadic
- HLAX0055 for variadic **float** arguments

## Struct by-value (MVP)

Record parameters at procedure boundaries are passed as **hidden pointers**.
