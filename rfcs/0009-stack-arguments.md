# RFC 0009 — Stack arguments (Phase 17 Sprint 1)

**Status:** Implemented (Phase 17 Sprint 1)  
**Since:** 0.1.0-alpha

## Summary

Integer/scalar procedure parameters and call arguments beyond the register limit are passed on the stack per the target ABI.

| ABI | Register args | Stack args (first slot) |
|-----|---------------|---------------------------|
| SysV AMD64 | `rdi`, `rsi`, `rdx`, `rcx`, `r8`, `r9` (6) | 7th at `[rbp+16]` in callee |
| Windows x64 | `rcx`, `rdx`, `r8`, `r9` (4) | 5th at `[rbp+48]` in callee |

## Procedure definitions

All parameters still receive `[rbp-N]` home slots via `ProcedureStackMap`. On entry:

1. Register arguments are copied from ABI registers into their slots (unchanged).
2. Stack arguments are loaded from the caller's stack frame into their slots:
   - SysV: `[rbp+16 + 8*k]` for parameter index `6+k`
   - Windows: `[rbp+48 + 8*k]` for parameter index `4+k` (after 32-byte shadow/home space)

## Procedure calls

### SysV

- Args 1–6 → `rdi` … `r9`
- Args 7+ pushed **right-to-left** before `call`
- Stack aligned to 16 bytes before `call` (extra `sub rsp, 8` when an odd number of stack args)
- Caller cleans stack args after return

### Windows x64

- Args 1–4 → `rcx`, `rdx`, `r8`, `r9`
- Caller allocates `32 + 8*(n-4)` bytes on stack (shadow space + stack args)
- Stack args stored at `[rsp+32]`, `[rsp+40]`, …
- Shadow space and stack slots released after `call`

## Deferred (Phase 17 Sprint 2+)

- Floating-point / vector args (`xmm0`–`xmm7`)
- `extern` declarations and import libraries
- Function pointers and indirect calls
- Struct by-value passing
- Variadic functions

## Examples

- `examples/06-abi/stack-args-sysv.hla64` — eight-arg sum (Linux SysV)
- Windows: use `--target windows-x64-msabi` with five-or-more-arg procedures

## References

- [docs/abi-linux-x64.md](../docs/abi-linux-x64.md)
- System V AMD64 ABI psABI
- Microsoft x64 calling convention
