# Classic HLA vs HlaX64

HlaX64 is **inspired by** the educational ideas of Randall Hyde's High Level Assembly (HLA) and *The Art of Assembly Language*. HlaX64 is an **independent project** and is **not affiliated with or endorsed by Randall Hyde**.

## Comparison

| Area | Classic HLA | HlaX64 |
|------|-------------|--------|
| Primary target | x86 (32-bit era focus) | x86-64 |
| ABI | x86-era conventions | Linux SysV + Microsoft x64 |
| Backend | HLA toolchain | NASM (+ platform linker) |
| File extension | `.hla` | `.hla64` |
| Purpose | Assembly education & development | AI-friendly verified native code |
| Language compatibility | Original HLA | Inspired subset / new language |
| Platforms | Historically Windows-centric | Linux + Windows |
| Standard library | HLA Standard Library (large) | Growing subset (`stdout`, argv, heap, file I/O, string/mem) |
| Interop | Varies by ecosystem | C ABI export, C header + C# P/Invoke generators |
| Agent tooling | — | MCP server, JSON CLI, test/bench runners |

## Operand order

Both use **HLA-style** `mov(source, dest)`. HlaX64 lowers to NASM `mov dest, source` in the backend.

## Example topics

Classic HLA teaching progression (arrays, strings, procedures, control flow) informs the [examples/](../examples/) layout. Implementations in this repository are **clean-room** — written for HlaX64 syntax and x64 ABI, not copied from HLA example archives.

## Third-party source

Do **not** copy Randall Hyde's example source into this repository unless license and redistribution rights are explicit. See [CONTRIBUTING.md](rules/CONTRIBUTING.md) and use [THIRD_PARTY_NOTICES.md](rules/THIRD_PARTY_NOTICES.md) when importing licensed material.

## Acknowledgement

> HlaX64 is inspired by the educational ideas of Randall Hyde's High Level Assembly language and *The Art of Assembly Language*. HlaX64 is an independent project and is not affiliated with or endorsed by Randall Hyde.

Learn from classic HLA materials. Credit them. Reimplement concepts in HlaX64. Do not copy uncertainly licensed source.
