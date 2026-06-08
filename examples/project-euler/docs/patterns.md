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

## Bignum digit-array add (PE #13)

Store each number little-endian in `nums[]` (`nums[n*50 + d]`). Accumulate into `sum[]` with carry in `r9`:

- Save the **sum digit index** in `r13` before `mod`/`idiv` — do not reuse the `nums[]` index register for `sum[i]`.
- Keep the **inner loop limit** in a register that is **not** reused as the `mod`/`idiv` divisor (e.g. limit in `r8`, divisor in `r15`; reload `mov(50, r8)` after each digit if needed).
- Backup the running total in **`r10`** before `mod`, restore before `idiv` (same as PE #16/#20).
- MSB scan: use `while(r15 > 0)` with `mov(0, r15)` to stop — **not** `mov(r15, r14)` (that is `r14 = r15`; operand order is `mov(source, destination)`).
- Build the top-10 answer: ten iterations of `r13 = r13*10 + sum[r14]; sub(1, r14)`.

Regenerate: `python scripts/generate_euler013.py` (embeds `data/euler013-numbers.txt`, checksum `5537376230`).

## Name scores × rank (PE #22)

Sort names alphabetically in the generator; embed precomputed letter scores (`A=1`…`Z=26`) in `scores[]`. Runtime loop:

```hla
mov(scores[i], rax);   // letter sum
mov(rank, r10);
imul(r10, rax);        // rax = score × rank (1-based)
add(rax, total);
```

Total fits in `int64` for the official `p022_names.txt` (5163 names → `871198282`). Regenerate: `python scripts/generate_euler022.py`.

## Lehmer permutations (PE #24)

Keep the factorial divisor in **`r11`** (not `r10`) when chaining `idiv` / `mod` with `idx` in `r10`. A `mod(f, r10)` before `idiv` can clobber the divisor register and corrupt the quotient on the next iteration.

When picking a digit, save the pool index to **`r8`** before any `imul(10, …)` — then `used[i]=1` via `mov(rax, used[r8])`, not `used[r15]` after `r15` becomes `10`.

Save **`idx` in `r15`** before updating `k` with `imul(f, idx)` — the product overwrites the index register if you reuse it for multiply.

**Workaround in `euler024`:** Lehmer indices for `k=999999` are embedded in `idxs[]` (same values as repeated `k / pos!`); the used-digit scan and accumulate are full runtime logic.

### Compiler/runtime follow-up (do not forget)

`idiv` in a **multi-iteration loop** with `mov(rdx, k)` (or equivalent remainder handoff) still produced wrong quotients/remainders in PE #24, even when:

- isolated `idiv` smoke tests passed (e.g. `32319 / 5040 → 6`);
- generated NASM looked correct;
- factorial lived in `r11`, idx in `r10`, and `mod`/`idiv` were not mixed on the same divisor register.

Symptoms: wrong permutation digits (e.g. `2789154360` / nine-digit variants vs expected `2783915460`). Precomputed `idxs[]` fixes the answer; root cause is **kandidat perbaikan compiler/runtime terpisah** — worth a minimal repro outside Project Euler before changing `euler024` again.

## Procedure calls in long loops

Heavy `call` loops (e.g. PE #23 divisor sums) may corrupt stack state after thousands of iterations. Prefer **inlining** hot helpers when a procedure result drifts after many calls.
