# RFC 0004 — Expressions and compile-time constants

| Field | Value |
|-------|-------|
| **Status** | Partially implemented (Phase 16 Sprint 1–2) |
| **Authors** | HlaX64 maintainers |
| **Created** | 2026-06-07 |

## Summary

Formalize compile-time constant blocks and a limited runtime expression assignment model (`:=`). Phase 16 Sprint 1 delivers **const blocks** and **const expression evaluation**; Phase 16 Sprint 2 delivers **runtime `:=` assignments** for int64 scalars.

## Motivation

- Remove magic numbers from source and generated code.
- Enable `type[ConstName]` array sizes and immediate operands derived from named constants.
- Provide a precedence table and signed integer rules before adding runtime expressions.

## Const blocks (implemented)

### Syntax

```hla
const
    BufferSize := 4096;
    PageSize   := 4096;
    PageMask   := PageSize - 1;
endconst;
```

- Allowed at **program** scope (before `begin`) and **procedure** scope (before `var`).
- Assignment uses `:=` (not `=`).
- Names are case-insensitive; duplicates in the same scope are errors (**HLAX0034**).
- Forward references within a block are not allowed (**HLAX0031**).

### Const expression grammar

Operands:

- Decimal integer literals
- Hex literals `$FF` (HLA-style)
- Parentheses
- References to constants already defined in the current scope chain (program + procedure)

Unary operators:

| Op | Meaning |
|----|---------|
| `-` | Negation |
| `~` | Bitwise NOT |

Binary operators (precedence high → low):

| Level | Operators |
|-------|-----------|
| Multiplicative | `*`, `/`, `%` |
| Additive | `+`, `-` |
| Shift | `<<`, `>>` |
| Bitwise AND | `&` |
| Bitwise XOR | `^` |
| Bitwise OR | `\|` |

All expressions are evaluated as **signed int64** at compile time.

### Overflow and division

| Condition | Code | Behavior |
|-----------|------|----------|
| Undefined const name | HLAX0031 | Error |
| Division or modulo by zero | HLAX0032 | Error |
| `+`, `-`, `*` overflow int64 | HLAX0033 | Error (checked arithmetic) |
| `/`, `%`, shifts, bitwise | — | Uses unchecked int64 / masked shift count |

### Uses (implemented)

- Integer immediate operands: `mov(BufferSize, rax)`
- Array sizes: `var buf: byte[BufferSize];`
- Array indices when the index is a const name (folded for bounds warnings and lowering)

## Runtime expressions (implemented — Sprint 2)

```hla
result := (a + b) * 4;
mask := value & $FF;
rax := a == b;
```

### Precedence

Same operator levels as const expressions, plus comparison operators (`==`, `!=`, `<`, `<=`, `>`, `>=`) at a lower precedence than bitwise OR.

### Operands

- int64 scalar locals and 64-bit registers (l-values)
- R-values: locals, registers, decimal/hex literals, const names, parentheses

### Lowering

Integer locals and registers only; lowered to IR ops (`Move`, `Add`, `Subtract`, `Multiply`, `Divide`, `Modulo`, bitwise, shifts, `CompareToBool`) before ABI lowering to NASM.

**Register clobbering:** evaluating a runtime expression uses **`rax` and `rbx` as scratch registers**. Their values are not preserved across a `:=` statement unless the assignment target is that same register and no subexpression reads it afterward. Do not rely on `rax`/`rbx` holding a value across `:=` lines.

### Diagnostics

| Condition | Code |
|-----------|------|
| Invalid assignment target | HLAX0035 |
| Unknown name in expression | HLAX0036 |
| Array used as expression operand | HLAX0037 |
| Literal/const zero divisor in expression | HLAX0038 |

### Deferred

- Float expressions
- Implicit widening/narrowing beyond current type rules
- L-value assignments to memory operands

## Signed / unsigned rules

- **Const evaluation:** all arithmetic is signed int64 unless a future `uint64` suffix is added.
- **Runtime expressions (planned):** operand types follow declared local/register types; mixed-width rules follow RFC 0001 type system updates.

## Runtime expression errors (implemented)

See diagnostics HLAX0035–HLAX0038 in [docs/diagnostics.md](../docs/diagnostics.md).

## Acceptance criteria

### Const blocks (Sprint 1) — done when:

- [x] Grammar in `docs/language-spec.md`
- [x] Lexer tokens: `const`, `endconst`, `:=`, hex `$..`, expression operators
- [x] Parser + precedence tests
- [x] Semantic evaluator + HLAX0031/32/33/34
- [x] IR lowering to `imm:N`
- [x] Conformance valid + invalid cases
- [x] Example under `examples/`
- [x] CHANGELOG entry

### Runtime expressions — done when:

- [x] `:=` assignment statement parsing
- [x] Type-checking for locals/registers
- [x] IR snapshot tests
- [x] NASM lowering for `+ - * / % & | ^ ~ << >>` and comparisons
- [x] Signed comparison tests (`== != < <= > >=`)
- [x] LSP + formatter support
- [x] Document `rax`/`rbx` clobbering during expression eval

## References

- Audit Tier A §5.1 (expressions), §5.2 (constants)
- [docs/language-spec.md](../docs/language-spec.md)
- [docs/diagnostics.md](../docs/diagnostics.md)
