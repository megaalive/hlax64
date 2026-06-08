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
- **Element types (0.x):** `byte`/`int8`/`uint8` (1 byte), `word`/`int16`/`uint16` (2), `dword`/`int32`/`uint32` (4), `int64`/`uint64`/`qword`/`ptr` (8).
- **Index:** register, local identifier, or non-negative integer literal.
- **Lowering:** `data[i]` → `[rbp+baseDisp + i×elementSize]` with sized `mov`/`movzx` for sub-qword elements (SIB when index is a register).

## Not in scope (0.x)

- Dynamic length / `new`
- Multi-dimensional arrays
- Runtime bounds checking (optional `-Wbounds` static warning — see `docs/diagnostics.md` HLAX0030)

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0024 | Array element type not supported (must be byte/word/dword/qword class) |
| HLAX0025 | Invalid array length (must be ≥ 1) |
| HLAX0026 | Indexed access on non-array variable |
| HLAX0027 | Unknown array variable in `arr[i]` |

## Examples

See `examples/curriculum/05-memory/array-*.hla64`.
