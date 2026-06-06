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

## Toolchain messages (no code yet)

CLI and linker errors currently use plain text (e.g. `Error: NASM not found`). Future releases will assign `HLAX4xxx` codes.

## Adding a diagnostic

1. Pick the next code in the appropriate category.
2. Emit via `DiagnosticCollection` in the compiler.
3. Add a unit test in `HlaX64.Compiler.Tests`.
4. Document the code in this file.
5. Mention in CHANGELOG if user-visible.

See [CONTRIBUTING.md](../CONTRIBUTING.md#adding-diagnostics).
