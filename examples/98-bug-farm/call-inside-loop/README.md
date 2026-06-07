# call-inside-loop

Minimal loop that calls a helper on every iteration. Mirrors the pattern used in `10-real-tools/` (hexdump, wc, fnv1a) where Win64 calls clobber volatiles.

## Stress

- `call` inside `while`
- Loop counter in `r8`, bound in `r10` (volatile — safe across calls on Win64)

## Build

```powershell
hla64 build examples/98-bug-farm/call-inside-loop/call-inside-loop.hla64 --target linux-x64-sysv
```
