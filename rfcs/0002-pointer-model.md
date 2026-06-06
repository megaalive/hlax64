# RFC 0002 — Pointer Model

- **Status:** Partially implemented (0.x)
- **Author:** HlaX64 maintainers

## Summary

Define how `ptr` types, address-of, dereference, and memory operations behave in HlaX64.

## Motivation

Level 3 curriculum (arrays, pointers, strings) requires a clear memory model before full array/index syntax.

## Implemented (minimal)

- `ptr` is a 64-bit address type alias.
- **Address-of:** `&ident` where `ident` is a local variable or procedure parameter in the current scope.
  - Example: `mov(&slot, rcx)` lowers to `lea rcx, [rbp-N]`.
- **Load through pointer:** `[reg]` where `reg` holds an address.
  - Example: `mov([rcx], rax)` lowers to `mov rax, [rcx]`.
- **Store through pointer:** `mov(src, [reg])` lowers to `mov [reg], src` (register or immediate source).

See [examples/05-memory/pointer-load-store.hla64](../examples/05-memory/pointer-load-store.hla64).

## Not yet implemented

- Array types and indexed access (`arr[i]`).
- Pointer arithmetic (`ptr+8`, byte offsets).
- Bounds checking or `hla64_memcpy` runtime integration.

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0022 | `[..]` operand must be a register holding an address |
| HLAX0023 | `&ident` must refer to a local variable or parameter |

## Compatibility

Additive; existing programs without pointer syntax are unchanged.

## Open questions

- Bounds checking strategy (trap vs documented UB)
- Indexed array syntax vs explicit `[base+offset]` forms
