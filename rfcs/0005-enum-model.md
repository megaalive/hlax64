# RFC 0005: Enum Model

**Status:** Implemented (Phase 16 Sprint 3)

## Summary

Program-scoped typed enumerations with compile-time integer members, usable as immediates in instructions, `:=` expressions, and array sizes.

## Syntax

```hla
enum Color: uint32
    Red   := 1;
    Green := 2;
    Blue  := 3;
endenum;
```

## Semantics

- Declared at program scope before `begin` (alongside `const`, `record`, `procedure`).
- Backing types: `uint32`, `int32`, `uint64`, `int64`.
- Member values are compile-time integer expressions (same evaluator as `const`).
- Qualified access: `Color.Red` resolves to the member value at compile time.
- Members are registered in the compile-time constant table as `EnumName.MemberName`.

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0039 | Duplicate enum type or member |
| HLAX0040 | Invalid enum backing type |
| HLAX0041 | Undefined enum member |

## Non-goals (0.x)

- Implicit auto-increment members
- Scoped enums inside procedures
- String enums
