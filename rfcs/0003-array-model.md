# RFC 0003 — Stack Array Model

- **Status:** Implemented (0.x)
- **Author:** HlaX64 maintainers
- **Depends on:** [RFC 0002 — Pointer Model](0002-pointer-model.md)

## Summary

Stack-allocated arrays with `name: type[count]` declarations and `name[index]` load/store syntax.

## Syntax

```hla
var data: int64[5];
var idx: int64;
begin P;
    mov(10, data[0]);
    mov(1, idx);
    mov(20, data[idx]);
    mov(data[2], rax);
end P;
```

- **Declaration:** `ident: type[count];` where `count` is a positive integer literal.
- **Element types (0.x):** `int64`, `uint64`, `qword`, `ptr` (64-bit / 8-byte slots).
- **Index:** register, local identifier, or non-negative integer literal.
- **Lowering:** `data[i]` → `[rbp+baseDisp + i×8]` (SIB when index is a register).

## Not in scope (0.x)

- Dynamic length / `new`
- Multi-dimensional arrays
- `byte[N]` packed arrays (use `[reg+off].byte` manual pattern)
- Bounds checking (documented UB — see `docs/memory-and-bounds.md`)

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0024 | Array element type not supported (must be 64-bit class) |
| HLAX0025 | Invalid array length (must be ≥ 1) |
| HLAX0026 | Indexed access on non-array variable |
| HLAX0027 | Unknown array variable in `arr[i]` |

## Examples

See `examples/05-memory/array-*.hla64`.
