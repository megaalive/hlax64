# Memory model & bounds (Level 3)

> **Status:** Normative for 0.x pointer/memory syntax · arrays/index types still planned

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

## Arrays (not yet)

- No `array` type or `arr[i]` syntax.
- Use consecutive locals + `[base + index×8]` until RFC 0003 (planned) lands.

## Bounds checking

**Policy (0.x):** No compile-time or runtime bounds checks on pointer dereference or manual indexing.

- Reading/writing outside allocated stack slots or string/rodata limits is **undefined behavior (UB)**.
- Future options (RFC 0002 open questions): debug trap (`int3`), `-Wbounds` warning mode, or documented UB only.

**Safe patterns today:**

1. Keep index ranges in loop conditions (see [string-length.hla64](../examples/05-memory/string-length.hla64)).
2. Prefer fixed-size manual arrays with known element count.
3. Run `hla64 test` on examples before shipping generated code.

## Runtime helpers (planned)

`hla64_str_len`, `hla64_memcpy`, `hla64_memset` — see [runtime-contract.md](runtime-contract.md).

## See also

- [tutorials/05-memory.md](tutorials/05-memory.md)
- [rfcs/0002-pointer-model.md](../rfcs/0002-pointer-model.md)
