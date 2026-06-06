# RFC 0007 — Program-scope static / global data

**Status:** Implemented (Phase 16 Sprint 5)  
**Since:** 0.1.0-alpha

## Summary

Program-scope `static` / `endstatic` blocks declare global symbols emitted to `.data` (initialized) or `.bss` (zero/uninitialized).

## Syntax

```hla
static
    counter: uint64 := 0;
    initialized: int64 := 42;
    buffer: byte[256];
endstatic;
```

## Semantics

| Declaration | Section | NASM |
|-------------|---------|------|
| Scalar with `:= expr` | `.data` | `dq` / `dd` / `dw` / `db` |
| Scalar/array without initializer | `.bss` | `resq` / `resb` / … |

- Program scope only (procedure-scoped globals deferred).
- Read/write via `mov(1, counter)`, `mov(counter, rax)`.
- Address-of via `mov(&counter, rax)` → `lea rax, [counter]`.
- Indexed global arrays use the same `name[index]` syntax as stack arrays.

## Diagnostics

| Code | Cause |
|------|-------|
| HLAX0045 | Duplicate static or conflict with const |
| HLAX0046 | Unknown static type |
| HLAX0048 | Invalid static initializer |
| HLAX0049 | Static name conflicts with procedure or type |

## Lowering

- IR encodes globals as `global:name`.
- SysV and Windows lowerers emit RIP-relative `[name]` memory operands.
