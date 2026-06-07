# Real Tools

Small programs that behave like everyday command-line tools. These examples are intentionally more realistic than arithmetic or ABI samples: they touch OS APIs, buffers, file metadata, loops over external data, integer formatting, and error paths.

The first batch targets Windows x64 because that is where `listfiles` exposed several compiler/runtime gaps. The paths are hardcoded until the language grows argv support.

## Tools

| Tool | What it does | Stress points |
|------|--------------|---------------|
| `listfiles` | Lists fixture file names and sizes | Win32 `FindFirstFileA`, struct offsets, `.dword` loads, `shl`, nested loops, multi-arg `stdout.put` |
| `filesize` | Prints one fixture file size | file metadata, 64-bit size combine, integer stdout |
| `exists` | Returns success when a fixture exists | minimal Win32 interop, branch, exit code |
| `linecount` | Counts newline bytes in a fixture file | `CreateFileA`, `ReadFile`, static buffers, byte walking, while+if loop |

## Build

```powershell
hla64 build examples/10-real-tools/listfiles/listfiles.hla64 --target windows-x64-msabi -o build/real-listfiles
hla64 build examples/10-real-tools/linecount/linecount.hla64 --target windows-x64-msabi -o build/real-linecount
```

## Regression Coverage

Two layers of regression cover these tools:

1. Compile-only manifests under `tests/examples-curriculum/real-*` lock that every tool still compiles. (The generic `hla64 test` runner assembles as ELF64/SysV, so these Win32 programs are checked in compile-only mode there.)

```powershell
hla64 test tests/examples-curriculum --filter real- --compile-only
```

2. Native execution regression (`RealTool_builds_and_runs_natively_on_windows` in `HlaX64.AssemblyLab.Tests`) builds each tool to a Windows `.exe`, runs it against the committed fixtures, and asserts stdout and exit code. It is skipped automatically when not on Windows or when no Windows linker is available.

All four tools (`exists`, `filesize`, `listfiles`, `linecount`) build and run natively on Windows.
