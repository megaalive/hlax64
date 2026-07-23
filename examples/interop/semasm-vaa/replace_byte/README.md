# replace_byte — SemASM / VAA bridge leaf

Replace every occurrence of `needle` with `replacement` in
`buffer[0..length]`; return the number of bytes replaced. Export symbol
`replace_byte` matches the SemASM contract `replace_byte.sem.toml` and VAA
task `replace-byte-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/replace_byte/replace_byte.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_replace_byte/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-replace_byte.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
- `replace_byte` writes to `buffer` (not read-only, unlike `memcmp`/`find_last_byte`).
