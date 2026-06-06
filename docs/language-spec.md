# HlaX64 Language Specification (Draft)

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

## Instructions (Planned)

| Instruction | Description          |
|-------------|----------------------|
| `mov`       | Move source to dest  |
| `add`       | Add source to dest   |
| `sub`       | Subtract source from dest |
| `imul`      | Signed multiply      |
| `idiv`      | Signed divide        |
| `xor`       | Bitwise XOR          |
| `and`       | Bitwise AND          |
| `or`        | Bitwise OR           |
| `cmp`       | Compare              |
| `jmp`       | Unconditional jump   |
| `je`        | Jump if equal        |
| `jne`       | Jump if not equal    |
| `jg`        | Jump if greater      |
| `jl`        | Jump if less         |
| `call`      | Call procedure       |
| `ret`       | Return from procedure|

## Control Flow (Planned)

### If/Else
```hla
if(condition) then
    // statements
else
    // statements
endif;
```

### While
```hla
while(condition) do
    // statements
endwhile;
```

## Procedures (Planned)

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;
```

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

### Windows x64 (Microsoft ABI) — Future

| Argument | Register |
|----------|----------|
| 1st      | `rcx`    |
| 2nd      | `rdx`    |
| 3rd      | `r8`     |
| 4th      | `r9`     |
| Return   | `rax`    |

## Target Pragma

```hla
#pragma target("linux-x64-sysv")
```

```hla
#pragma target("windows-x64-msabi")