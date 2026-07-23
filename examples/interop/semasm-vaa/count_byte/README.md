# count_byte — SemASM / VAA bridge leaf

Count occurrences of `needle` in `buffer[0..length]`; return the count. Export
symbol `count_byte` matches the SemASM contract `count_byte.sem.toml` and VAA
task `count-byte-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/count_byte/count_byte.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_count_byte/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-count_byte.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
- `count_byte` only reads `buffer` (read-only, like `memcmp`/`find_last_byte`/`find_first_byte`).
