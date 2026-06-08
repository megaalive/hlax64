# HlaX64 patterns for Project Euler

Reusable idioms used across `problems/`. Operand order for `mov`/`add`/`sub`: **`mov(source, destination)`** (second operand is written).

## Modulo (`mod`)

```hla
mov(i, rax);
mod(3, rax);   // rax = i % 3
if(rax = 0) then
    // divisible by 3
endif;
```

Lowering uses `idiv`; dividend must be in `rax`. Remainder lands back in `rax`.

**Divisor must be a register** (not an immediate): `mov(3, r10); mod(r10, rax)`.

**Loop bounds:** keep the limit in `rbx` (or reload after each `mod`/`idiv`) — `idiv` clobbers `rdx`.

## Integer division (`idiv`)

```hla
mov(n, rax);
idiv(k, rax);  // rax = n / k  (signed)
```

## Sum multiples below limit (formula)

For positive `k` and `limit`:

\[
\sum_{i=1}^{\lfloor (limit-1)/k \rfloor} ki = k \cdot m(m+1)/2 \quad\text{where } m = \lfloor (limit-1)/k \rfloor
\]

See `euler001-multiples-of-3-and-5-formula.hla64`.

## Fibonacci step

```hla
mov(a, r11);
mov(b, r12);
add(r12, r11);   // r11 = a + b
mov(r12, a);     // a = old b
mov(r11, b);     // b = new value
```

## Trial division factor

```hla
mov(n, rax);
mod(factor, rax);
if(rax = 0) then
    mov(n, rax);
    idiv(factor, rax);
    mov(rax, n);
else
    add(1, factor);
endif;
```

## stdout.put with integers

On Windows library mode, pass **registers** to `stdout.put` for numeric locals:

```hla
mov(total, rdx);
stdout.put("Answer: ", rdx, nl);
```

## Record / extern calls

- Compare record fields via register: `mov(v.len, rdx); if(rcx < rdx)`
- Extern calls: load fields to registers first, `call hlax_realloc(r11, r12)`

## Multiply (`imul`)

`imul(factor, dest)` lowers to `imul dest, factor` — **the product stays in `dest`**.

```hla
mov(1, r14);
mov(digits[i], rax);
imul(rax, r14);   // r14 *= rax — do NOT mov(rax, r14) afterward
```

## Avoid mem–mem

The MVP backend does not emit mem-to-mem `mov`/`cmp`/`add`. Always bounce through a register.
