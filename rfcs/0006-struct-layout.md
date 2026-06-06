# RFC 0006: Struct / Record Layout

**Status:** Implemented (Phase 16 Sprint 4)

## Summary

Program-scoped `record` types with deterministic natural alignment, stack-allocated variables, dot field access, and compile-time `sizeof` / `offsetof` builtins.

## Syntax

```hla
record PatientHeader
    version: uint16;
    flags: uint16;
    length: uint32;
    timestamp: uint64;
endrecord;

procedure P; @returns("rax");
var header: PatientHeader;
begin P;
    mov(10, header.length);
end P;
```

## Layout rules (0.x)

- Fields laid out in declaration order.
- Each field aligned to its natural size (platform-neutral default).
- Padding inserted between fields as required.
- Total record size rounded up to maximum field alignment.
- `packed` attribute deferred to a future sprint.

## Builtins

- `sizeof(RecordName)` — total size in bytes
- `offsetof(RecordName, field)` — byte offset of a field

## Code generation

- Record variables occupy a single stack blob (`IrLocalLayout` sized to record bytes).
- Field access lowers to `[rbp-offset+fieldOffset]` with size-appropriate `mov` (`.word`, `.dword`, etc.).

## Diagnostics

| Code | Meaning |
|------|---------|
| HLAX0042 | Unknown record type |
| HLAX0043 | Unknown record field |
| HLAX0044 | Invalid `offsetof` (unknown record) |
