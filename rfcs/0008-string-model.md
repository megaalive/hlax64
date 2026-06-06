# RFC 0008 — String model (`cstring`, `utf8slice`)

**Status:** Implemented (Phase 16 Sprint 5)  
**Since:** 0.1.0-alpha

## Summary

HlaX64 uses explicit string types instead of a vague `string` keyword.

## `cstring`

| Property | Value |
|----------|-------|
| Representation | Alias of `ptr` — null-terminated UTF-8 |
| Literals | `&"text"` and rodata labels |
| Parameters | `procedure F(msg: cstring);` |

Lowering is identical to `ptr`; the keyword documents intent for C ABI interop.

## `utf8slice`

Built-in record layout (16 bytes, natural alignment):

```hla
// conceptual
record utf8slice
    ptr: ptr;
    len: uint64;
endrecord;
```

Use `var s: utf8slice;` and field access `s.ptr`, `s.len`. Pass pointer and length as separate procedure parameters for MVP (`data: ptr; len: uint64`).

## Deferred (Phase 17+)

- Single-parameter `utf8slice` ABI lowering (two registers)
- Owned/mutable string types
- `strlen` / slice builtins
