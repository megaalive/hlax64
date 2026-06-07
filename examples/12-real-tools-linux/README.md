# Real Tools (Linux)

Linux SysV ports of selected command-line tools using `libc.so` instead of Win32 APIs. Each tool mirrors layout from `examples/10-real-tools/` (`fixtures/`, `expected.*`, `hla64.toml` with `linux-x64-sysv`).

## Tools

| Tool | What it does | Stress points |
|------|--------------|---------------|
| `linecount` | Count `\n` bytes in `argv[1]` | Linux argv runtime, `open`/`read`/`close`, while+if |

## Build

```bash
hla64 build examples/12-real-tools-linux/linecount/linecount.hla64 --target linux-x64-sysv -o build/linux-linecount
```

Run from the **repository root** so relative fixture paths in `expected.arguments` resolve under WSL.

## Regression

- **Compile-only** — `tests/examples-curriculum/linux-linecount`
- **Native WSL** — `LinuxRealTool_linecount_runs_under_wsl_when_available` (skipped when WSL/linker unavailable)

## Roadmap

More Linux ports (`exists`, `wc`, `fnv1a`) can follow the same argv + libc pattern.
