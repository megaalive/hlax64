# Memory & pointers (Level 3)

Tutorial for RFC 0002 extensions: address-of, indexed memory, sized loads, and string traversal.

**Prerequisites:** [01-getting-started.md](01-getting-started.md), procedures with `var` blocks.

---

## 1. Address-of stack variables

Take the address of a local or parameter with `&name`:

```hla
var slot: int64;
mov(&slot, rcx);    // lea rcx, [rbp-N]
mov([rcx], rax);    // load 64-bit value through rcx
```

See [pointer-load-store.hla64](../../examples/curriculum/05-memory/pointer-load-store.hla64).

---

## 2. Store through a pointer

Operand order is HLA-style: `mov(source, destination)`.

```hla
mov(99, [rcx]);     // store immediate into memory at rcx
```

See [pointer-store.hla64](../../examples/curriculum/05-memory/pointer-store.hla64).

---

## 3. Stack arrays (RFC 0003)

Declare fixed-size stack arrays and index them:

```hla
var data: int64[5];
mov(10, data[0]);
mov(1, idx);
mov(data[idx], rax);
```

See [array-sum.hla64](../../examples/curriculum/05-memory/array-sum.hla64), [array-max.hla64](../../examples/curriculum/05-memory/array-max.hla64), [rfcs/0003-array-model.md](../../rfcs/0003-array-model.md).

### Packed element types

Use `byte[N]`, `word[N]`, or `dword[N]` when each slot is smaller than 64 bits (1-, 2-, or 4-byte stride):

```hla
var buf: byte[4];
mov(10, buf[0]);
mov(40, buf[3]);    // last byte — exit code in array-byte-last.hla64
```

See [array-byte-last.hla64](../../examples/curriculum/05-memory/array-byte-last.hla64).

---

## 4. Manual stack arrays (`[base + offset]`)

Before `type[N]` syntax, you could model a small array as consecutive `int64` locals and index with a byte offset. This pattern still works when you need raw pointer arithmetic:

```hla
var elem0: int64;
var elem1: int64;
mov(&elem0, rcx);
mov([rcx], rax);        // elem0
mov([rcx + 8], rbx);    // elem1 (next 8-byte slot)
```

See [stack-array.hla64](../../examples/curriculum/05-memory/stack-array.hla64) — sums three elements (exit 60).

---

## 5. Sized memory access (`.byte`, `.word`, `.dword`, `.qword`)

Append a size suffix after `]` for partial-width loads/stores:

```hla
mov([rcx].byte, rax);       // movzx rax, byte [rcx]
mov([rcx + 4].dword, rbx);  // mov ebx, dword [rcx+4]
```

Aliases match [language-spec.md](../language-spec.md): `byte` (8-bit), `word` (16), `dword` (32), `qword` (64, default).

See [typed-byte.hla64](../../examples/curriculum/05-memory/typed-byte.hla64).

---

## 6. String literals and byte traversal

Address of read-only string data:

```hla
mov(&"hello", rcx);     // lea rcx, [str_N]
mov([rcx].byte, ch);    // first character
```

Walk until `\0`:

```hla
while(ch > 0) do
    add(1, rax);        // length counter
    add(1, rcx);        // next byte
    mov([rcx].byte, ch);
endwhile;
```

See [string-length.hla64](../../examples/curriculum/05-memory/string-length.hla64) — exit code 5.

---

## 7. Bounds and safety

Read [memory-and-bounds.md](../memory-and-bounds.md). HlaX64 **does not** enforce runtime bounds in 0.x — out-of-range access is undefined behavior.

Optional **static** warnings for **literal** indices:

```bash
hla64 build examples/curriculum/05-memory/array-sum.hla64 -Wbounds
```

Emits **HLAX0030** when a literal index is `< 0` or `>=` array length. Register/variable indices are not analyzed. The language server enables bounds warnings in diagnostics by default.

---

## 8. Next steps

- Level 4 ABI: [06-abi examples](../../examples/curriculum/06-abi/)
- Interop: [03-csharp-interop.md](03-csharp-interop.md)
- RFC: [0002-pointer-model.md](../../rfcs/0002-pointer-model.md)

Run curriculum tests:

```bash
hla64 test tests/examples-curriculum/
```
