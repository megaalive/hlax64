# Real Tools (Linux)

Linux SysV ports of selected command-line tools using `libc.so` instead of Win32 APIs. Each tool mirrors layout from `examples/tools/10-windows/` (`fixtures/`, `expected.*`, `hla64.toml` with `linux-x64-sysv`).

## Tools

| Tool | What it does | Stress points |
|------|--------------|---------------|
| `linecount` | Count `\n` bytes in `argv[1]` | Linux argv runtime, `open`/`read`/`close`, while+if |
| `exists` | Exit 0 when path exists | `access(path, F_OK)`, exit code |
| `wc` | Lines / words / bytes | Byte classifiers, calls in loop, multi-arg `stdout.put` |
| `fnv1a` | FNV-1a 64-bit file hash | xor/imul loop, **`stdout.putu`** unsigned decimal |

## Build

```bash
hla64 build examples/tools/12-linux/wc/wc.hla64 --target linux-x64-sysv -o build/linux-wc
```

Run from the **repository root** so relative fixture paths in `expected.arguments` resolve under WSL.

## Regression

- **Compile-only** — `tests/examples-curriculum/linux-*`
- **Native WSL** — `LinuxRealTool_runs_under_wsl_when_available` (skipped when WSL/linker unavailable)

## stdout.putu

Use `stdout.putu(...)` when printing **unsigned** 64-bit values (hashes, addresses, bit patterns). Signed decimals still use `stdout.put(...)`.
