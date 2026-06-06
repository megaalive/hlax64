# Changelog

All notable changes to HlaX64 are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/) after 1.0; during 0.x, breaking changes are allowed with documentation.

## [Unreleased]

### Added

- GitHub Pages landing page (`docs/index.html`) with links to playground, install, and tutorials
- Level 3 memory curriculum: `[reg+offset]`, sized `.byte`/`.word`/`.dword`, `&"string"`, four new examples
- Tutorial [docs/tutorials/05-memory.md](docs/tutorials/05-memory.md) and [docs/memory-and-bounds.md](docs/memory-and-bounds.md)
- Native samples `stack_array`, `string_length`; four new curriculum manifests (24 total)
- Minimal pointer ops per RFC 0002: `&var`, `[reg]` load/store; HLAX0022/HLAX0023 diagnostics
- Example `examples/05-memory/pointer-load-store.hla64` with sample, curriculum, and conformance tests
- Windows CI smoke: run built `.exe` (exit code) and build `export_lib` shared `.dll`
- GitHub issue backlog script (`scripts/create-issue-backlog.ps1`); 20 curated issues (#1–#20)
- Native sample `comparison_uint64_boundary` (uint64 high-bit vs 1); fixes #2
- `scripts/setup-toolchain-path.ps1` for Windows NASM/MinGW PATH setup

### Fixed

- Parser accepts `int64` minimum literal (`-9223372036854775808`)
- SysV exit epilogue uses `rbx` when `mov` targets `rbx` (restores exit-code path)

### Changed

- Remove README demo GIF and `scripts/generate-demo-gif.py`

## [0.1.0-alpha] - 2026-06-06

First public alpha: compiler toolchain, MCP server, structured examples, CI, and installable release archives.

### Added

- HLA-inspired `.hla64` compiler: lexer, parser, semantic analysis, IR, ABI lowering
- NASM backend for `linux-x64-sysv` and `windows-x64-msabi`
- Runtime library with stdout helpers and runtime contract markers
- CLI: `build`, `emit-nasm`, `run`, `test`, `bench`, `explain`, `explain-abi`, `format`, `generate-header`, `generate-pinvoke`, `doctor`
- MCP server with 12 tools (compile, build, run, test, explain, format-source, doctor, interop generators, …)
- JSON CLI output with `schemaVersion` and JSON schemas under `schemas/`
- Community files: LICENSE, CONTRIBUTING, GOVERNANCE, CHANGELOG, issue/PR templates, CI + release workflows
- Structured `examples/` curriculum (20 `.hla64` programs) with compile guards in CI
- `tests/examples-curriculum/` — 18 native test manifests pointing at curriculum sources
- Conformance suite and parser robustness tests (117+ unit tests)
- `hla64 explain`, `hla64 format`, VS Code grammar skeleton, RFC docs
- `HlaX64.LanguageServer` diagnostics MVP and static playground page
- Docs: [install.md](docs/install.md), [architecture.md](docs/architecture.md), tutorials under [docs/tutorials/](docs/tutorials/)
- GitHub Pages workflow for `docs/` (playground, tutorials)
- Release archives: Linux `.tar.gz`, Windows `.zip`, checksums, dependency SBOM JSON
- `.githooks/prepare-commit-msg` to strip unwanted co-author trailers (optional)

### Changed

- README and docs aligned with Linux + Windows targets, MCP, and feature status
- `DoctorCommand` and explain/format services shared between CLI and MCP

[Unreleased]: https://github.com/megaalive/hlax64/compare/v0.1.0-alpha...HEAD
[0.1.0-alpha]: https://github.com/megaalive/hlax64/releases/tag/v0.1.0-alpha
