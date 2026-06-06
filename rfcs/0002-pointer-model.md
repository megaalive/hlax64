# RFC 0002 — Pointer Model (Draft)

- **Status:** Draft
- **Author:** HlaX64 maintainers

## Summary

Define how `ptr` types, address-of, dereference, and memory operations will behave in HlaX64.

## Motivation

Level 3 curriculum (arrays, pointers, strings) requires a clear memory model before implementation.

## Proposed direction

- `ptr` is 64-bit address type (alias exists today).
- Address-of (`&var`) and load/store through pointers in a later 0.x release.
- No implicit pointer arithmetic without explicit size suffix (future `ptr+8` vs byte offsets TBD).

## Compatibility

Additive when implemented; no current user code relies on pointers beyond the type name.

## Open questions

- Bounds checking strategy (trap vs UB documentation)
- Interaction with `hla64_memcpy` runtime when added
