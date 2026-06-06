# HlaX64 Language Specification

> **Language version:** 0.1  
> **Specification status:** Draft  
> **Compiler compatibility:** HlaX64 0.x  
> **Last updated:** 2026-06-07  
> **Targets:** `linux-x64-sysv` (default), `windows-x64-msabi` (via `--target`)

## Overview

HlaX64 is an HLA-inspired x64 assembly language designed for AI coding agents and humans. It provides a higher-level syntax over raw NASM/MASM while remaining close to the metal.

## File Extension

`.hla64` — This extension distinguishes HlaX64 source from classic HLA `.hla` files.

## Program Structure

```hla
program programName;

#include("stdlib64.hhf")

begin programName;
    // instructions
end programName;
```

### Rules

- `program` must be the first keyword.
- The program name after `program` must match the name after `begin` and `end`.
- `#include` directives appear before any `begin` block.
- Statements end with semicolons.

## Types

| Type     | Description        | Size    |
|----------|--------------------|---------|
| `int8`   | Signed 8-bit       | 1 byte  |
| `int16`  | Signed 16-bit      | 2 bytes |
| `int32`  | Signed 32-bit      | 4 bytes |
| `int64`  | Signed 64-bit      | 8 bytes |
| `uint8`  | Unsigned 8-bit     | 1 byte  |
| `uint16` | Unsigned 16-bit    | 2 bytes |
| `uint32` | Unsigned 32-bit    | 4 bytes |
| `uint64` | Unsigned 64-bit    | 8 bytes |
| `byte`   | Alias for `uint8`  | 1 byte  |
| `word`   | Alias for `uint16` | 2 bytes |
| `dword`  | Alias for `uint32` | 4 bytes |
| `qword`  | Alias for `uint64` | 8 bytes |
| `ptr`    | Pointer            | 8 bytes |

## Registers

### 64-bit
```text
rax rbx rcx rdx
rsi rdi rbp rsp
r8  r9  r10 r11 r12 r13 r14 r15
```

### 32-bit
```text
eax ebx ecx edx
esi edi ebp esp
r8d r9d r10d r11d r12d r13d r14d r15d
```

### 16-bit
```text
ax bx cx dx
```

### 8-bit
```text
al bl cl dl
```

## Instructions

| Instruction | Description          | Status   |
|-------------|----------------------|----------|
| `mov`       | Move source to dest  | ✅ MVP   |
| `add`       | Add source to dest   | ✅ MVP   |
| `sub`       | Subtract source from dest | ✅ MVP |
| `imul`      | Signed multiply      | ✅ MVP   |
| `xor`       | Bitwise XOR          | ✅ MVP   |
| `and`       | Bitwise AND          | ✅ MVP   |
| `or`        | Bitwise OR           | ✅ MVP   |
| `cmp`       | Compare              | ✅ MVP   |
| `idiv`      | Signed divide        | ⏳ planned |
| `jmp`       | Unconditional jump   | ⏳ planned |
| `je` / `jne`| Jump if equal / not equal | ✅ MVP (via `if`/`while`) |
| `jg` / `jl` | Jump if greater / less | ✅ MVP (via `if`/`while`) |
| `call`      | Call procedure       | ✅ MVP (basic) |
| `ret`       | Return from procedure| ✅ MVP (auto-emitted) |

> Operand order follows HLA convention: `mov(source, dest)`. The NASM
> backend reverses this to NASM's `mov dest, source`.

## Standard Library: `stdout.put`

`stdout.put` is the only standard library call available in MVP. It accepts
a comma-separated list of arguments and prints each in order.

### Signature

```hla
stdout.put(arg1, arg2, ...);
```

### Supported argument types

| Argument form         | Behaviour                                   |
|-----------------------|---------------------------------------------|
| `"literal"`           | Print the literal string verbatim           |
| `nl`                  | Print a newline (`0x0A`)                    |
| `register`            | Print the register as a signed decimal int  |
| `integer literal`     | Print the literal as decimal text           |

### Examples

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

```hla
program greet;

begin greet;
    mov(42, rax);
    stdout.put("answer=", rax, nl);
end greet;
```

### Escape sequences in string literals

| Escape | Meaning   |
|--------|-----------|
| `\\`   | backslash |
| `\"`   | quote     |
| `\n`   | newline   |
| `\r`   | CR        |
| `\t`   | tab       |

### Implementation notes

- The MVP compiler inlines `sys_write` calls; the stable runtime functions
  `stdout_put_str`, `stdout_put_nl`, `stdout_put_int`, and `int_to_str`
  live in `src/HlaX64.Runtime/linux-x64/` and are documented in
  `src/HlaX64.Runtime/include/stdlib64.hhf`.
- Generated NASM contains `; RUNTIME: <function-name>` comments at each
  inlined call site to mark the future integration point.

## Control Flow (Implemented)

### If/Else

```hla
if(condition) then
    // statements
else
    // statements
endif;
```

Supported comparison operators: `=`, `<`, `>`.

### While

```hla
while(condition) do
    // statements
endwhile;
```

## Procedures (Implemented)

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;
```

Up to 6 integer parameters are passed in `rdi`, `rsi`, `rdx`, `rcx`,
`r8`, `r9`. Return value goes in `rax` (specified via `@returns`).

## Compile-time constants (Implemented — RFC 0004)

Declare named integer constants evaluated at compile time:

```hla
const
    BufferSize := 4096;
    PageSize   := 4096;
    PageMask   := PageSize - 1;
endconst;
```

- Placement: program scope (before `begin`) or procedure scope (before `var`).
- Assignment operator is `:=` (distinct from comparison `=`).
- Expressions: `+`, `-`, `*`, `/`, `%`, `&`, `|`, `^`, `~`, `<<`, `>>`, parentheses, unary `-`.
- Literals: decimal integers and hex `$FF`.
- Uses: immediate operands (`mov(BufferSize, rax)`), array sizes (`var buf: byte[BufferSize];`).
- Errors: undefined name (`HLAX0031`), divide by zero (`HLAX0032`), overflow (`HLAX0033`), duplicate (`HLAX0034`).

## Runtime expressions (Implemented — RFC 0004 Sprint 2)

Assign to int64 scalar locals or 64-bit registers:

```hla
var a: int64;
var b: int64;
a := 10;
b := (a + 3) * 2;
rax := a == b;
mask := value & $FF;
```

- Operators: `+`, `-`, `*`, `/`, `%`, `&`, `|`, `^`, `~`, `<<`, `>>`, and comparisons `==`, `!=`, `<`, `<=`, `>`, `>=`.
- R-values: locals, registers, decimal/hex literals, const names, parentheses.
- **Expression evaluation clobbers `rax` and `rbx`** as scratch registers; do not assume they retain values across `:=` statements.
- Errors: invalid target (`HLAX0035`), unknown name (`HLAX0036`), array in expression (`HLAX0037`), zero divisor (`HLAX0038`).

See [RFC 0004](../rfcs/0004-expressions-and-constants.md).

## Enumerations (Implemented — RFC 0005)

Program-scoped typed enums with compile-time member values:

```hla
enum Color: uint32
    Red   := 1;
    Green := 2;
    Blue  := 3;
endenum;

mov(Color.Red, rax);
```

- Backing types: `uint32`, `int32`, `uint64`, `int64`.
- Members use the same compile-time expression evaluator as `const`.
- Qualified access `EnumName.Member` resolves as an immediate.
- Errors: duplicate type/member (`HLAX0039`), invalid backing type (`HLAX0040`), undefined member (`HLAX0041`).

See [RFC 0005](../rfcs/0005-enum-model.md).

## Records (Implemented — RFC 0006)

Program-scoped record types with natural alignment and dot field access:

```hla
record PatientHeader
    version: uint16;
    length: uint32;
endrecord;

var header: PatientHeader;
mov(42, header.length);
```

- Layout: fields in order, align each field to its size, pad, round total size to max alignment.
- Compile-time builtins: `sizeof(RecordName)`, `offsetof(RecordName, field)`.
- Stack allocation: one blob per `var` (sized via `IrLocalLayout`).
- Errors: unknown record (`HLAX0042`), unknown field (`HLAX0043`), invalid `offsetof` (`HLAX0044`).
- **Packed:** `record Name packed` — no inter-field padding (minimum field sizes only).

Procedure-scoped `enum` and `record` blocks may appear before `var` (same placement as `const`).

See [RFC 0006](../rfcs/0006-struct-layout.md).

## Static / global data (Implemented — RFC 0007)

Program-scope globals via `static` / `endstatic`:

```hla
static
    counter: uint64 := 0;
    buffer: byte[256];
endstatic;

mov(1, counter);
mov(counter, rax);
mov(&counter, rax);
```

Initialized scalars → `.data`; uninitialized → `.bss`. Diagnostics HLAX0045–HLAX0049.

See [RFC 0007](../rfcs/0007-global-data.md).

## String model (Implemented — RFC 0008)

- **`cstring`** — alias of `ptr`; null-terminated UTF-8 (`var p: cstring;`, `procedure F(msg: cstring);`).
- **`utf8slice`** — built-in record `{ ptr, len }`; use field access and pass `(ptr, len)` to procedures.

Enum members may omit `:= value` for auto-increment (`Red := 1; Green; Blue;` → 1, 2, 3).

See [RFC 0008](../rfcs/0008-string-model.md).

## Memory & pointers (Implemented — Level 3 baseline)

See [tutorials/05-memory.md](tutorials/05-memory.md) and [memory-and-bounds.md](memory-and-bounds.md).

```hla
var slot: int64;
var data: int64[8];
mov(&slot, rcx);
mov(data[0], rax);
mov(10, data[1]);
```

- `&ident` requires a local variable or parameter in the current procedure (`HLAX0023`).
- `[reg]` requires a register holding an address; optional `+ byteOffset` and size suffix.
- Array declaration and `arr[i]` are **implemented** — see [RFC 0003](../rfcs/0003-array-model.md). Element types: `byte`/`word`/`dword`/`int64`/`uint64`/`qword`/`ptr` (and int/uint aliases).
- Optional compile-time bounds warnings for literal indices: CLI `-Wbounds` (HLAX0030); see [diagnostics.md](diagnostics.md).
- Use consecutive locals + `[base + N]` only when avoiding array syntax.

## Calling Convention

### Linux x64 (System V ABI)

| Argument | Register |
|----------|----------|
| 1st      | `rdi`    |
| 2nd      | `rsi`    |
| 3rd      | `rdx`    |
| 4th      | `rcx`    |
| 5th      | `r8`     |
| 6th      | `r9`     |
| Return   | `rax`    |

### Windows x64 (Microsoft ABI) — Implemented

| Argument | Register |
|----------|----------|
| 1st      | `rcx`    |
| 2nd      | `rdx`    |
| 3rd      | `r8`     |
| 4th      | `r9`     |
| Return   | `rax`    |

32-byte shadow space required; `--target windows-x64-msabi` flag selects
Windows ABI lowering (NASM `win64` format + `lld-link`/`link.exe`).

## Target Pragma

```hla
#pragma target("linux-x64-sysv")
```

```hla
#pragma target("windows-x64-msabi")
```

---

## Normative vs non-normative text

Sections describing **implemented** syntax are normative. Planned features are informative until marked implemented in [roadmap.md](roadmap.md).

## Reserved keywords

```text
program begin end procedure call export var const endconst
if else endif while endwhile do then
mov add sub imul xor and or cmp lea push pop inc dec neg not
shl shr sar rol ror jmp ret syscall nop int3 hlt
include pragma target
int8 int16 int32 int64 uint8 uint16 uint32 uint64 byte word dword qword ptr
true false null nil nl stdout stderr stdin
```

## Overflow and signedness

- Untyped register arithmetic wraps at the operand width (two's complement).
- Typed variables use `int*` (signed) and `uint*` (unsigned) for semantic checks.
- Implicit narrowing assignments are rejected (`HLAX0021`).

## Undefined behavior

Not fully diagnosed in v0.1: uninitialized reads, stack misalignment before foreign calls, out-of-bounds pointer/index access (see [memory-and-bounds.md](memory-and-bounds.md)).

## Compatibility

See [compatibility.md](compatibility.md) and [diagnostics.md](diagnostics.md).

