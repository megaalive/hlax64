# HlaX64 ABI Reference: Linux x64 (System V)

This document describes the **Linux x86-64 System V ABI** as it applies to
HlaX64 procedures. It is the contract the compiler and the user must
follow when calling a `procedure` or implementing one.

> Source of truth for the upstream ABI:
> <https://refspecs.linuxfoundation.org/elf/x86_64-abi-0.99.pdf> (PDF).
> The notes here are condensed and focus on what HlaX64 actually emits
> and consumes.

## 1. Argument passing

Integer / pointer arguments are passed in **registers**, left-to-right
in source order. The first six arguments do not touch the stack.

| Source position | Register |
|-----------------|----------|
| 1st argument    | `rdi`    |
| 2nd argument    | `rsi`    |
| 3rd argument    | `rdx`    |
| 4th argument    | `rcx`    |
| 5th argument    | `r8`     |
| 6th argument    | `r9`     |
| 7th+            | stack, right-to-left pushed by caller |

> Floating-point arguments use `xmm0..xmm7` (not used in MVP).

The HlaX64 MVP supports up to **six** integer arguments. A seventh or
later argument is not yet lowered (it is a Fase 6+ follow-up).

## 2. Return value

| Type of return   | Location       |
|------------------|----------------|
| 64-bit integer   | `rax`          |
| 64-bit pointer   | `rax`          |
| 32-bit integer   | `eax` (zero-extends to `rax`) |

HlaX64 procedures declare the return register with `@returns("rax")`:

```hla
procedure Add(a:int64; b:int64); @returns("rax");
begin Add;
    mov(a, rax);
    add(b, rax);
end Add;
```

## 3. Caller / callee saved registers

The System V ABI classifies registers into two groups:

### Caller-saved (volatile)

If a procedure uses any of these, it must save and restore them itself
if it needs the original value after a call.

```
rax  rcx  rdx  rsi  rdi  r8  r9  r10  r11
```

### Callee-saved (non-volatile)

A procedure may freely use these, but it must preserve their values
across the call. The standard prologue/epilogue handles `rbp`.

```
rbx  rbp  r12  r13  r14  r15
```

> HlaX64 MVP does not yet emit explicit save/restore for callee-saved
> registers other than `rbp`. Use only caller-saved registers inside
> the procedure body unless you are prepared for the clobber.

## 4. Stack frame

At the entry to a procedure, the stack looks like:

```
   high address
   +------------------+
   | return address   |  <- rsp at entry (caller pushed)
   |------------------|
   | saved rbp        |  <- after `push rbp`  (also new rbp)
   |------------------|
   | [rbp-N]  local N |
   |   ...            |
   | [rbp-8]  param 0 |  <- a (rdi saved here)
   | [rbp-16] param 1 |  <- b (rsi saved here)
   +------------------+
   low address
```

The HlaX64 backend emits the following sequence on procedure entry:

```asm
MyProc:
    push rbp
    mov  rbp, rsp
    ; 1. store incoming register args into [rbp-8], [rbp-16], ...
    mov  [rbp-8],  rdi
    mov  [rbp-16], rsi
    ; 2. if there are `var` declarations, `sub rsp, N*8` for them
    ; 3. procedure body
    pop  rbp
    ret
```

Parameter and variable offsets are assigned in declaration order:

| Slot | Offset    | Holds                  |
|------|-----------|------------------------|
| 0    | `[rbp-8]` | 1st param (came from `rdi`) |
| 1    | `[rbp-16]`| 2nd param (came from `rsi`) |
| 2    | `[rbp-24]`| 3rd param (came from `rdx`) |
| …    | …         | …                      |
| 6    | `[rbp-56]`| 1st `var`              |
| 7    | `[rbp-64]`| 2nd `var`              |
| …    | …         | …                      |

The compiler currently generates **load-on-use** addressing: every time
the source references `a`, the backend emits `mov a, rax` → `mov rax,
[rbp-8]`. There is no live-range-based register allocation in MVP.

## 5. Stack alignment

The System V ABI requires `rsp` to be **16-byte aligned** at the point
of a `call` instruction. HlaX64 takes a simple approach:

```asm
    sub  rsp, 8      ; align stack to 16 bytes before the call
    call AddTwo
    add  rsp, 8      ; restore stack alignment
```

This is emitted unconditionally around `call` instructions from the
main body. It is correct as long as `rsp` is 16-byte aligned at the
main program's `_start` (which the Linux kernel guarantees) and as
long as the body doesn't independently perturb `rsp`.

## 6. System calls

The MVP uses raw `syscall` instructions for I/O. Linux x64 system
calls follow the same calling convention as user code, except:

- The syscall number goes in `rax` (not in `rdi`).
- The first argument goes in `rdi`, second in `rsi`, and so on.
- `rcx` and `r11` are clobbered.

| Syscall | rax  | rdi        | rsi         | rdx        |
|---------|------|------------|-------------|------------|
| `read`  | 0    | fd         | buf pointer | count      |
| `write` | 1    | fd         | buf pointer | count      |
| `exit`  | 60   | exit code  | —           | —          |

HlaX64's `stdout.put` uses `write(1, buf, len)` (fd=1) and the
implicit exit at the end of `_start` uses `exit(rax)` where `rax`
holds the desired exit code (set by `mov imm, rax` at the end of the
program body).

## 7. Quick example

```hla
// add_two.hla64 — full working example
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;

program main;
begin main;
    call AddTwo(10, 20);     // 10 -> rdi, 20 -> rsi
    stdout.put("sum = ", rax, nl);   // rax = 30
end main;
```

Generated NASM (abridged):

```asm
AddTwo:
    push rbp
    mov  rbp, rsp
    mov  [rbp-8],  rdi
    mov  [rbp-16], rsi
    mov  rax, [rbp-8]
    add  rax, [rbp-16]
    pop  rbp
    ret

_start:
    push rbp
    mov  rbp, rsp
    mov  rdi, 10
    mov  rsi, 20
    sub  rsp, 8
    call AddTwo
    add  rsp, 8
    ; ... stdout.put emits sys_write syscalls ...
    mov  rdi, rax
    mov  rax, 60
    syscall
```

## 8. See also

- [`docs/language-spec.md`](./language-spec.md) — language reference
- `src/HlaX64.Runtime/linux-x64/` — hand-written runtime that follows
  this ABI
- Upstream System V x86-64 ABI specification
