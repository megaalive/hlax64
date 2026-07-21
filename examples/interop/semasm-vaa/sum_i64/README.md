# sum_i64 — SemASM / VAA bridge leaf

Wrapping sum of `int64` elements at `values[0..length]`. Export symbol `sum_i64`
matches the SemASM contract `sum_i64.sem.toml` and VAA task `sum-i64-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/sum_i64/sum_i64.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_sum_i64/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-sum_i64.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
