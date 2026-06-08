# Memory model & bounds (Level 3)

> **Status:** Normative for 0.x pointer/memory syntax  
> **See also:** [memory-model.md](memory-model.md) (formal string/pointer/array overview)

## Stack locals

- Each `var` / parameter occupies an **8-byte stack slot** in the current procedure frame (`[rbp-N]`), regardless of declared type name (`byte`, `int64`, …).
- Typed names affect **semantic narrowing checks** between identifiers; they do not yet change slot size or alignment rules.

## Pointer operations (implemented)

| Syntax | Meaning | NASM (typical) |
|--------|---------|----------------|
| `&ident` | Address of local/param | `lea reg, [rbp-N]` |
| `&"text"` | Address of rodata string | `lea reg, [str_N]` |
| `[reg]` | Load/store qword | `mov …, [reg]` |
| `[reg + N]` | Indexed qword access | `mov …, [reg+N]` |
| `[reg + N].byte` | 8-bit access | `movzx` / `mov byte` |
| `[reg + N].word` | 16-bit | `movzx` / `mov word` |
| `[reg + N].dword` | 32-bit | `mov` / `mov dword` |

Diagnostics: [HLAX0022](diagnostics.md#hlax0022--invalid-memory-dereference) (legacy), [HLAX0023](diagnostics.md#hlax0023--invalid-address-of-operand).

Non-register inside `[..]` is a **parse error** in current releases.

## Arrays (RFC 0003)

- Stack arrays: `var data: type[N];` with `data[i]` indexing — see [RFC 0003](../rfcs/0003-array-model.md).
- Packed element types (`byte[N]`, `word[N]`, `dword[N]`) use the correct byte stride in lowering.

## Bounds checking

**Policy (0.x):** No runtime bounds checks. Out-of-range access is **undefined behavior (UB)**.

- **Static warnings:** `-Wbounds` / `--warn-bounds` on `build`, `emit-nasm`, `run`, and `explain` emits **HLAX0030** when a **literal** index may be out of range. Register/variable indices are not analyzed.
- The language server enables bounds warnings in diagnostics by default.
- Future options: debug trap (`int3`), full static analysis, or documented UB only.

**Safe patterns today:**

1. Keep index ranges in loop conditions (see [string-length.hla64](../examples/curriculum/05-memory/string-length.hla64)).
2. Prefer fixed-size manual arrays with known element count.
3. Run `hla64 test` on examples before shipping generated code.

## Runtime helpers (planned)

`hla64_str_len`, `hla64_memcpy`, `hla64_memset` — see [runtime-contract.md](runtime-contract.md).

## See also

- [tutorials/05-memory.md](tutorials/05-memory.md)
- [rfcs/0002-pointer-model.md](../rfcs/0002-pointer-model.md)
