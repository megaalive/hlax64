# Memory

Stack locals, pointers, manual arrays, and string byte-walk (Level 3 curriculum).

| Example | Exit | Description |
|---------|------|-------------|
| [sum-1-to-5.hla64](sum-1-to-5.hla64) | — | Register-only loop (no pointers) |
| [pointer-load-store.hla64](pointer-load-store.hla64) | 42 | `&slot` + load via `[rcx]` |
| [pointer-store.hla64](pointer-store.hla64) | 99 | Store immediate through `[rcx]` |
| [stack-array.hla64](stack-array.hla64) | 60 | Three slots via `[base+offset]` |
| [typed-byte.hla64](typed-byte.hla64) | 65 | `[reg].byte` sized load |
| [string-length.hla64](string-length.hla64) | 5 | `&"hello"` + byte traversal |
| [array-sum.hla64](array-sum.hla64) | 15 | RFC 0003: sum `int64[5]` |
| [array-fill.hla64](array-fill.hla64) | 3 | Fill array in loop |
| [array-max.hla64](array-max.hla64) | 9 | Max of `int64[4]` |
| [array-literal-index.hla64](array-literal-index.hla64) | 20 | Literal index `arr[1]` |
| [array-byte-last.hla64](array-byte-last.hla64) | 40 | Packed `byte[4]` with 1-byte stride |

Tutorial: [docs/tutorials/05-memory.md](../../docs/tutorials/05-memory.md) · Bounds: [docs/memory-and-bounds.md](../../docs/memory-and-bounds.md)

Stack arrays: [RFC 0003](../../rfcs/0003-array-model.md). Optional bounds warnings: `-Wbounds` (see [diagnostics.md](../../docs/diagnostics.md#hlax0030--possible-out-of-bounds-array-index)).
