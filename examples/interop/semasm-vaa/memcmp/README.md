# memcmp — SemASM / VAA bridge leaf

Unsigned lexicographic compare of `a[0..length]` vs `b[0..length]`.
Returns `-1`, `0`, or `1` (fail-closed). Export symbol `memcmp` matches the
SemASM contract `memcmp.sem.toml` and VAA task `memcmp-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/memcmp/memcmp.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_memcmp/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-memcmp.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
