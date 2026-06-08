# RFC 0002 — Pointer Model

- **Status:** Partially implemented (Level 3 curriculum baseline)
- **Author:** HlaX64 maintainers

## Summary

Pointer types, address-of, dereference, indexed addressing, and sized memory ops for Level 3 curriculum.

## Implemented

| Feature | Example |
|---------|---------|
| `ptr` type alias (64-bit) | `var p: ptr;` (decl only) |
| `&var` / `&param` | `mov(&slot, rcx)` → `lea` |
| `&"literal"` | `mov(&"hi", rcx)` → rodata `lea` |
| `[reg]` load/store (64-bit) | `mov([rcx], rax)` |
| `[reg + offset]` | `mov([rcx + 8], rbx)` |
| Sized access `.byte`/`.word`/`.dword`/`.qword` | `mov([rcx].byte, rax)` |

Examples under `examples/curriculum/05-memory/`. Tutorial: `docs/tutorials/05-memory.md`.

## Not yet implemented

- Array types and `arr[i]` — see [RFC 0003](../rfcs/0003-array-model.md)
- Pointer arithmetic on `ptr` variables (`p + 8` as typed op)
- Bounds checking (documented UB — see `docs/memory-and-bounds.md`)
- `hla64_memcpy` / `hla64_str_len` runtime (planned)

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0022 | Legacy: non-register inside `[..]` (now usually parse-time) |
| HLAX0023 | `&ident` must be local variable or parameter |

## Open questions

- RFC 0003: native array types vs manual `[base+index×size]`
- Bounds trap vs UB-only documentation
