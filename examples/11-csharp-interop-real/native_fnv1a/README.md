# native_fnv1a

Exports `Fnv1a64(data, length)` — the same FNV-1a loop as `10-real-tools/fnv1a`, as a shared library for C#.

## Expected

With `fixtures/sample-a.txt` (`alpha\n`): stdout `fnv1a=-4912795366963963885`, exit `0`.

Hash uses signed `imul` wraparound (same NASM as the export). The `10-real-tools/fnv1a` exe prints the negated decimal via `stdout.put` for this fixture — the interop example uses the raw `int64` returned in `rax`.
