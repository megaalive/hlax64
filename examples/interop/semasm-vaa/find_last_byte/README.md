# find_last_byte — SemASM / VAA bridge leaf

Return the last index of `needle` in `buffer[0..length]`, or `length` when
absent. Export symbol `find_last_byte` matches the SemASM contract
`find_last_byte.sem.toml` and VAA task `find-last-byte-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/find_last_byte/find_last_byte.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_find_last_byte/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-find_last_byte.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
