# HlaX64 Diagnostic Catalog

Stable diagnostic codes help agents, IDE tooling, and contributors reference errors consistently.

## Code categories

| Range | Category |
|-------|----------|
| `HLAX0xxx` | General / semantic (current 0.x codes) |
| `HLAX1xxx` | Lexer / parser (reserved) |
| `HLAX2xxx` | Semantic / type system |
| `HLAX3xxx` | ABI / code generation (reserved) |
| `HLAX4xxx` | Linker / runtime / toolchain (reserved) |
| `HLAX5xxx` | MCP / integration (reserved) |

> **Note:** Pre-1.0 codes use a flat `HLAX00xx`/`HLAX02xx` scheme in the semantic analyzer. New diagnostics should follow the category ranges above; existing codes remain stable until 1.0.

## Current diagnostics

### HLAX0001 — Empty program name

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Semantic |
| **Since** | 0.1.0-alpha |
| **Cause** | `program` declaration has no name |
| **Example** | Invalid empty program header |
| **Fix** | Provide a valid program name matching `begin`/`end` |

### HLAX0003 — Unknown instruction

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Semantic |
| **Since** | 0.1.0-alpha |
| **Cause** | Mnemonic is not in the supported instruction set |
| **Example** | `movz( rax, rbx );` |
| **Fix** | Use a known mnemonic (`mov`, `add`, …). Suggestion may appear via fuzzy match |

### HLAX0004 — Wrong operand count

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Semantic |
| **Since** | 0.1.0-alpha |
| **Cause** | Instruction arity does not match specification |
| **Example** | `mov(rax);` (expects 2 operands) |
| **Fix** | Supply the correct number of operands |

### HLAX0007 — Duplicate procedure

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Semantic |
| **Since** | 0.1.0-alpha |
| **Cause** | Two procedures share the same name |
| **Fix** | Rename or remove the duplicate declaration |

### HLAX0012 — Unknown register

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Semantic |
| **Since** | 0.1.0-alpha |
| **Cause** | Register name is invalid or likely a typo |
| **Example** | `mov(1, raxz);` |
| **Fix** | Use a valid x64 register (`rax`, `rbx`, …) |

### HLAX1000 — Parse error

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Parser |
| **Since** | 0.1.0-alpha |
| **Cause** | Source does not match HlaX64 grammar |
| **Fix** | Correct syntax per [language-spec.md](language-spec.md) |

### HLAX0020 — Unknown type

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.1.0-alpha |
| **Cause** | Variable or parameter uses an unrecognized type name |
| **Example** | `var x:foobar;` |
| **Fix** | Use a type from [language-spec.md](language-spec.md) (`int64`, `uint64`, …). Applies to variables and parameters. |

### HLAX0021 — Implicit narrowing conversion

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.1.0-alpha |
| **Cause** | Storing a wider typed variable into a narrower one without explicit conversion |
| **Fix** | Widen the destination, narrow the source explicitly, or adjust types |

### HLAX0022 — Invalid memory dereference

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | Operand inside `[..]` is not a register holding an address (legacy semantic path); non-register forms are usually rejected at parse time |
| **Example** | `mov([42], rax);` — parse error in current releases |
| **Fix** | Use a register that holds a pointer, e.g. `mov([rcx], rax);` after `mov(&slot, rcx);` |

### HLAX0023 — Invalid address-of operand

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | `&ident` does not refer to a local variable or parameter in the current procedure |
| **Example** | `mov(&rax, rcx);` |
| **Fix** | Take the address of a `var` or parameter, e.g. `mov(&slot, rcx);` |

### HLAX0024 — Unsupported array element type

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | Array `type[count]` requires a supported element type (`byte`, `word`, `dword`, `int64`, `uint64`, `qword`, `ptr`, and signed/unsigned aliases) |
| **Fix** | Use a supported element type per [RFC 0003](../rfcs/0003-array-model.md) |

### HLAX0025 — Invalid array length

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | Array length in `type[count]` is less than 1 |
| **Fix** | Use a positive integer literal count |

### HLAX0026 — Index on non-array variable

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | `x[i]` applied to a scalar `var` (not declared as `type[count]`) |
| **Fix** | Declare `var arr: int64[N];` or index a manual pointer with `[reg+off]` |

### HLAX0027 — Unknown array in indexed access

| Field | Value |
|-------|-------|
| **Severity** | Error |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | `arr[i]` references an unknown name |
| **Fix** | Declare `arr` in the procedure `var` block |

### HLAX0030 — Possible out-of-bounds array index

| Field | Value |
|-------|-------|
| **Severity** | Warning |
| **Category** | Type system |
| **Since** | 0.2.0-alpha |
| **Cause** | Literal index in `arr[i]` is negative or `>=` declared array length (static check only) |
| **Example** | `var buf: int64[4];` … `mov(buf[4], rax);` with `-Wbounds` |
| **Fix** | Use a valid index (`0` … `length-1`), or guard dynamic indices at runtime |
| **CLI** | Enable with `-Wbounds` / `--warn-bounds` on `build`, `emit-nasm`, `run`, `explain`. The language server enables bounds warnings in diagnostics by default. |

## Toolchain messages (no code yet)

CLI and linker errors currently use plain text (e.g. `Error: NASM not found`). Future releases will assign `HLAX4xxx` codes.

## Adding a diagnostic

1. Pick the next code in the appropriate category.
2. Emit via `DiagnosticCollection` in the compiler.
3. Add a unit test in `HlaX64.Compiler.Tests`.
4. Document the code in this file.
5. Mention in CHANGELOG if user-visible.

See [CONTRIBUTING.md](../CONTRIBUTING.md#adding-diagnostics).
