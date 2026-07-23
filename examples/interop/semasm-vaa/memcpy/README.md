# memcpy — SemASM / VAA bridge leaf

Copy every byte of `src[0..length]` into `dst[0..length]`; always returns
status `0`. Export symbol `memcpy` matches the SemASM contract
`memcpy.sem.toml` and VAA task `memcpy-win64-v1`.

## Emit Win64 NASM for VAA (shared-library — no `_start`)

```bash
hla64 emit-nasm examples/interop/semasm-vaa/memcpy/memcpy.hla64 \
  --target windows-x64-msabi \
  --output-kind shared-library \
  -o ../vaa/fixtures/ingest/hlax64_memcpy/candidate.asm
```

Or from the VAA repo:

```powershell
./scripts/regen-hlax64-memcpy.ps1
```

## Honesty

- HlaX64 compile / `-Wverify` is **not** SemASM `verified`.
- VAA Gate-1 without `--allow-execution` expects Incomplete (`execution_denied`).
- Gate-2 Verified requires `vaa verify … --allow-execution` with SemASM on PATH.
- Prefer `--output-kind shared-library` so the emit has no `ExitProcess` entry stub.
- `memcpy` writes to `dst` and reads `src` (dual-pointer like `memcmp`, write
  like `replace_byte`/`memset`) — `dst`/`src` are assumed distinct,
  non-overlapping buffers (SemASM ADR 0003, "overlap fail-closed").
- Each `src` byte is loaded into a scratch register and stored straight into
  `dst` without routing through `rax`/`al`, but the constant status (`rax = 0`)
  is still only set once, after the copy loop, for the same reason as
  `memset`/`replace_byte`.
