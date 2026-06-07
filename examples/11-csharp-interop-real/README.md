# C# Interop Real Examples

Practical shared libraries exported from HlaX64 and called from C# via generated P/Invoke.

Each example follows:

```
example-name/
├── example-name.hla64    # export procedure(s), no Win32 stdout in the hot path
├── README.md
├── hla64.toml
├── fixtures/             # inputs for the C# caller
├── expected.stdout
├── expected.exitcode
└── caller/               # .NET console that loads the native DLL
    ├── Caller.csproj
    ├── Program.cs
    └── HlaX64Interop.cs
```

## Build native DLL (Windows)

```powershell
hla64 build examples/11-csharp-interop-real/native_count_lines/native_count_lines.hla64 `
  --target windows-x64-msabi --output-kind shared-library `
  -o build/native_count_lines
```

Copy `build/native_count_lines/libnative_count_lines.dll` next to the caller as `native_count_lines.dll` (Windows `LibraryImport` name without the `lib` prefix).

## Generate P/Invoke

```powershell
hla64 generate-pinvoke examples/11-csharp-interop-real/native_count_lines/native_count_lines.hla64 `
  -l native_count_lines -o examples/11-csharp-interop-real/native_count_lines/caller/HlaX64Interop.cs
```

## Run C# caller

```powershell
cd examples/11-csharp-interop-real/native_count_lines/caller
dotnet run -- ../fixtures/sample-b.txt
```

## Regression

- **Compile-only** — `tests/examples-curriculum/interop-*` manifests.
- **Native Windows** — `InteropReal_caller_runs_on_windows` builds the DLL, runs the caller, and checks `expected.stdout`, `expected.exitcode`, and optional `expected.arguments`.

## Examples

| Folder | Export | C# use case |
|--------|--------|-------------|
| `native_count_lines` | `CountLines(data, length)` | Count `\n` bytes in a file buffer (same logic as `10-real-tools/linecount`) |
| `native_fnv1a` | `Fnv1a64(data, length)` | FNV-1a 64-bit hash of a buffer (same logic as `10-real-tools/fnv1a`) |
| `native_sum_bytes` | `SumBytes(data, length)` | Sum of byte values — smallest interop sanity check |
