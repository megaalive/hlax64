# memset — SemASM / VAA bridge leaf

Fill every byte of `buffer[0..length]` with `value`; always returns status
`0` (void-as-count-0). Export symbol `memset` matches the SemASM contract
`memset.sem.toml` and VAA task `memset-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/memset/memset.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_memset/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-memset.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
- `memset` writes to `buffer` (not read-only, unlike `memcmp`/`find_last_byte`) —
  same write-shape bug class as `replace_byte`: the byte-store lowering can
  route the stored value through `rax`/`al` as scratch, so the constant
  status is only moved into `rax` once, after the loop.
