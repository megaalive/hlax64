# min_usize — SemASM / VAA bridge leaf

Return the smaller of two unsigned sizes. Export symbol `min_usize` matches
the SemASM contract `min_usize.sem.toml` and VAA task `min-usize-win64-v1`
(oracle `builtin.pure_int.binary_usize`, claim `min`).

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/min_usize/min_usize.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_min_usize/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-min_usize.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
- Pure-integer leaf: no buffer/pointer arguments, no memory effects declared.
- `min_usize`/`max_usize` share the same `(usize, usize) -> usize` calling
  shape; SemASM's `builtin.pure_int.binary_usize` oracle disambiguates `min`
  vs `max` from the contract name/`ensures`, not the wire layout.
