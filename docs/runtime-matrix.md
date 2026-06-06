# Runtime Target Matrix

Explicit defaults for each **target × output kind** combination. See [runtime-contract.md](runtime-contract.md) for clobber metadata.

| Target | Output kind | Runtime default | Entry point | Link libraries | Notes |
|--------|-------------|-----------------|-------------|----------------|-------|
| `linux-x64-sysv` | Executable | Inline syscalls (MVP) or `--runtime library` | `_start` | None (inline) or `HlaX64.Runtime` objects | Default target; WSL OK on Windows hosts |
| `linux-x64-sysv` | Shared library | Runtime library | None (exported symbols) | `gcc -shared` | Use `export procedure` |
| `linux-x64-sysv` | Assembly-only (`emit-nasm`) | None | N/A | None | No link step |
| `windows-x64-msabi` | Executable | Windows runtime | `_start` → `ExitProcess` | `HlaX64.Runtime` + `kernel32` | 32-byte shadow space |
| `windows-x64-msabi` | DLL | Windows runtime | `DllMain` (minimal) + exports | Same + export table | `--output-kind shared-library` |
| `windows-x64-msabi` | Assembly-only | None | N/A | None | NASM `win64` format |

## CLI flags

```bash
# Linux executable (default)
hla64 build program.hla64

# Linux shared library
hla64 build lib.hla64 --output-kind shared-library

# Windows executable
hla64 build program.hla64 --target windows-x64-msabi

# Windows DLL
hla64 build lib.hla64 --target windows-x64-msabi --output-kind shared-library

# NASM only
hla64 emit-nasm program.hla64
```

## Runtime modes

| Mode | Flag | Behavior |
|------|------|----------|
| Inline | `--runtime inline` (legacy MVP) | Syscalls / inline sequences where supported |
| Library | `--runtime library` (default for linked builds) | Calls `hla64_*` runtime functions with clobber contract |

## Dependencies by phase

| Phase | Linux exe | Linux `.so` | Windows exe | Windows DLL |
|-------|-----------|-------------|-------------|-------------|
| Compile | — | — | — | — |
| Assemble | NASM | NASM | NASM | NASM |
| Link | `gcc`/`ld` | `gcc -shared` | `lld-link`/`link` | same + `/DLL` |
| Run | Native or WSL | `dlopen` consumer | Native | LoadLibrary consumer |

Run `hla64 doctor` to verify toolchain availability on your machine.
