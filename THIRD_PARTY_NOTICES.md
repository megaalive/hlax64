# Third-Party Notices

HlaX64 is primarily MIT-licensed (see [LICENSE](../LICENSE)).

## Runtime and toolchain

| Component | License | Notes |
|-----------|---------|-------|
| NASM | BSD-2-Clause | User-installed assembler |
| GCC / Clang / LLD / MSVC link | Various | User-installed linkers |
| .NET SDK / NuGet packages | MIT and per-package | See `dotnet list package --include-transitive` |

## Educational inspiration

Example program **topics** may be inspired by classic High Level Assembly teaching materials (Randall Hyde). **Source code** in `examples/` and `tests/samples/` is original HlaX64 code unless explicitly noted otherwise in the file header.

See [docs/classic-hla-comparison.md](docs/classic-hla-comparison.md).

## Adding third-party code

Before importing external source:

1. Confirm license allows modification and redistribution compatible with MIT.
2. Preserve copyright and license notices.
3. Add an entry to this file.
4. Prefer `third_party/<name>/` for vendored assets.

Do not place uncertainly licensed ports in `examples/` without provenance.
