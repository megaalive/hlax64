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

## Large immediates (>32767)

Literals above ~32767 may truncate when loaded directly. Bounce through a register:

```hla
mov(362880, r10);
mov(r10, facts[9]);
mov(999999, r10);
mov(r10, r12);
```

Same for `5040`, `40320`, etc. in factorial tables.

## Save temps before `imul` / `stdout.put`

`imul(10, r11)` overwrites `r11`. If `r11` held a digit or array index, copy it to `r15` (or another scratch reg) **before** multiply.

Avoid `stdout.put` inside tight numeric loops on the current runtime — it clobbers callee-saved / scratch state and can corrupt later iterations.

## Lehmer permutations (PE #24)

Keep the factorial divisor in **`r11`** (not `r10`) when chaining `idiv` / `mod` with `idx` in `r10`. A `mod(f, r10)` before `idiv` can clobber the divisor register and corrupt the quotient on the next iteration.

When picking a digit, save the pool index to **`r8`** before any `imul(10, …)` — then `used[i]=1` via `mov(rax, used[r8])`, not `used[r15]` after `r15` becomes `10`.

Save **`idx` in `r15`** before updating `k` with `imul(f, idx)` — the product overwrites the index register if you reuse it for multiply.

## Procedure calls in long loops

Heavy `call` loops (e.g. PE #23 divisor sums) may corrupt stack state after thousands of iterations. Prefer **inlining** hot helpers when a procedure result drifts after many calls.
