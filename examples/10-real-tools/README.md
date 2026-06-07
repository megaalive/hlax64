# Real Tools

Small programs that behave like everyday command-line tools. These examples are intentionally more realistic than arithmetic or ABI samples: they touch OS APIs, buffers, file metadata, loops over external data, integer formatting, and error paths.

All seven tools use the Windows argv runtime (`hlax_argv_*`). File readers take a path as `argv[1]`; `listfiles` takes a glob (e.g. `fixtures\\*`).

## Tool layout

Each tool directory follows:

```
tool-name/
├── tool-name.hla64    # readable, commented source (not one-liners)
├── README.md
├── hla64.toml
├── fixtures/          # per-tool test inputs
├── expected.stdout    # native regression expectations
├── expected.exitcode
└── expected.arguments # optional CLI args for native regression (file tools)
```

## Tools

| Tool | What it does | Stress points |
|------|--------------|---------------|
| `listfiles` | Lists files matching `argv[1]` glob | argv runtime, FindFirstFileA, struct offsets, nested if/while |
| `filesize` | Prints one file size (`argv[1]`) | argv runtime, 64-bit size combine, metadata |
| `exists` | Existence check + exit code (`argv[1]`) | argv runtime, minimal Win32 interop |
| `linecount` | Counts `\n` bytes in a file (`argv[1]`) | argv runtime, CreateFileA (7 args), ReadFile, while+if |
| `hexdump` | Offset + hex bytes (`argv[1]`) | argv runtime, hex nibble branches, calls inside loop |
| `wc` | Lines / words / bytes (`argv[1]`) | argv runtime, byte classifiers, multiple counters |
| `fnv1a` | FNV-1a 64-bit file hash (`argv[1]`) | argv runtime, xor/imul loop, large constants |

## Build

```powershell
hla64 build examples/10-real-tools/hexdump/hexdump.hla64 --target windows-x64-msabi -o build/real-hexdump
```

Run from the **repository root** so relative fixture paths resolve.

## Regression coverage

1. **Compile-only** — `tests/examples-curriculum/real-*` (via `hla64 test tests/examples-curriculum --filter real- --compile-only`).
2. **Native Windows** — `RealTool_builds_and_runs_natively_on_windows` reads each tool's `expected.stdout` / `expected.exitcode` and optional `expected.arguments`.

## Known limitations (compiler/runtime)

- **`and` / `xor` / `or` in source**: fixed in IR lowering (were incorrectly emitted as `mov`); real tools include regression coverage.
- **Calls inside loops**: prefer callee-saved registers (`r14` cursor, `r13` end sentinel) — volatile regs (`r8`–`r11`) are clobbered by Win64 calls.
- **`stdout.put` integers**: printed as **signed** int64 (FNV hash displays as negative decimal for large unsigned values).
- **Static names**: avoid identifiers like `inWord` that confuse the NASM backend.

## Roadmap (from review)

| Phase | Item |
|-------|------|
| Done | Standardize tool layout + expected files |
| Done | Add `hexdump`, `wc`, `fnv1a` |
| Done | Skeleton `98-bug-farm/`, `99-invalid/` |
| Done | argv for all seven real-tools |
| Next | `filemagic`, `cmp`, C# interop folder `11-csharp-interop-real/` |
