# HlaX64 memory model (0.1.x)

> **Status:** Normative overview · aligns with audit Tier B and RFC 0002/0003  
> **See also:** [memory-and-bounds.md](memory-and-bounds.md) · [RFC 0002](../rfcs/0002-pointer-model.md) · [RFC 0003](../rfcs/0003-array-model.md)

This document formalizes how HlaX64 models memory at the language level. Items marked **implemented** ship in compiler 0.1.x; **planned** items are specified for Phase 16+ but not yet in the toolchain.

---

## 1. Stack frame (implemented)

| Concept | Behavior |
|---------|----------|
| Locals | `var` block; each scalar occupies an **8-byte slot** `[rbp-N]` in the current procedure |
| Parameters | Same slot layout as locals; SysV `rdi..r9` / Windows `rcx..r8..r9` for the first N integer args |
| Arrays | `type[N]` on stack; element stride from element type (`byte` = 1 byte, `int64` = 8 bytes, …) |
| Alignment | 16-byte stack alignment at call sites (ABI lowerer) |

Const sizes: `var buf: byte[BufferSize];` when `BufferSize` is a compile-time constant (**implemented**, RFC 0004).

### 1.1 Records on stack (implemented — RFC 0006)

| Concept | Behavior |
|---------|----------|
| Declaration | `record Name ... endrecord` at program scope; `var x: Name;` in a procedure |
| Layout | Natural alignment: field offset = align-up cursor; total size rounded to max field alignment |
| Field access | `var.field` lowers to `[rbp-slot+offset]` with size-appropriate `mov` |
| Builtins | `sizeof(Name)`, `offsetof(Name, field)` in const expressions |

**Implemented:** `packed record Name` (`record Name packed`) — no inter-field padding except minimum field size.

---

## 2. Pointers (implemented — Level 3)

| Type / syntax | Model | Status |
|---------------|-------|--------|
| `ptr` | 64-bit address in a register or 8-byte slot | Implemented |
| `&var` | Address of stack local/param | Implemented |
| `&"text"` | Address of rodata string label | Implemented |
| `[reg + offset]` | Untyped memory access with optional `.byte`/`.word`/`.dword`/`.qword` | Implemented |

**Planned:** `const ptr<T>`, `mut ptr<T>`, nullable pointers, typed pointer arithmetic (audit §5.5).

---

## 3. Arrays (implemented)

| Syntax | Model | Status |
|--------|-------|--------|
| `var a: int64[N];` | N contiguous 8-byte elements on stack | Implemented |
| `var b: byte[N];` | Packed N-byte buffer | Implemented |
| `a[i]` | Indexed access; stride from element type | Implemented |
| `-Wbounds` | Static warning HLAX0030 for literal / const indices | Implemented |

**Planned:** `slice<T>` (pointer + length), checked indexing mode (audit §5.10).

---

## 4. String model

### 4.1 Static string literals (implemented)

```hla
stdout.put("Hello", nl);
mov(&"text", rax);
```

- Encoding: **UTF-8** bytes in `.rodata`
- Lifetime: program lifetime (linker rodata)
- Ownership: compiler-managed labels; not freed
- Mutability: immutable

### 4.2 `cstring` (implemented — RFC 0008)

| Property | Value |
|----------|-------|
| Representation | Type alias of `ptr` — null-terminated UTF-8 |
| Length | Implicit (`strlen` style); not stored |
| Use | C ABI, syscalls, `stdout.put` string args |

String literals and `&"..."` assign to `cstring` variables and parameters.

### 4.3 `utf8slice` (implemented — RFC 0008)

| Property | Value |
|----------|-------|
| Representation | Built-in 16-byte record: `ptr: ptr; len: uint64` |
| Null termination | Not required |
| Use | Safer string walk; pass `ptr` + `len` as separate procedure args |

**Deferred:** single-parameter `utf8slice` ABI (two register args automatic).

### 4.4 Design rule

Do not introduce a single keyword `string` without specifying encoding, termination, length, ownership, and mutability. Prefer explicit `cstring` / `utf8slice` names.

---

## 5. Global data (implemented — RFC 0007)

| Section | Purpose | Status |
|---------|---------|--------|
| `.data` | Initialized globals (`:=` initializer) | Implemented |
| `.bss` | Zero/uninitialized globals | Implemented |
| `.rodata` | String literals (already emitted) | Partial (strings only) |

Syntax: `static` / `endstatic` at program scope. Read/write via `mov`, address-of via `&name`.

---

## 6. Heap (planned)

Allocator modes (`--allocator libc|os|custom`) and `malloc`/`free` lowering are specified in the audit but not implemented.

---

## 7. Undefined behavior

- Out-of-bounds array access without runtime checks (see [memory-and-bounds.md](memory-and-bounds.md))
- Dangling pointers when addresses escape stack frames (no escape analysis yet)

---

## 8. Phase 16 alignment

| Audit item | This document |
|------------|---------------|
| §5.11 String model | §4 (implemented) |
| Tier B intro (memory correctness) | §1–§5 implemented; §6+ planned |
| Const + array sizes | §1, RFC 0004 |
| §5.3 Enum | RFC 0005 (implemented) |
| §5.4 Struct / record | §1.1, RFC 0006 (implemented) |
| §5.8 Global data | §5, RFC 0007 (implemented) |
