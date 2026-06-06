# Changelog

All notable changes to HlaX64 are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/) after 1.0; during 0.x, breaking changes are allowed with documentation.

## [Unreleased]

### Added

- Open-source community foundation: LICENSE, GOVERNANCE, issue templates, contributor docs
- CLI machine-readable output (`--json`) with `schemaVersion` for build, test, bench, doctor, explain-abi, generate-header, generate-pinvoke
- JSON Schema definitions under `schemas/`
- Structured example programs under `examples/`
- Runtime target matrix documentation
- Windows native build step in CI
- Release workflow for downloadable CLI archives

### Changed

- README and docs aligned with current capabilities and compatibility policy
- Diagnostic catalog documented in `docs/diagnostics.md`

## [0.1.0-alpha] - 2026-06-06

### Added

- HLA-inspired `.hla64` compiler: lexer, parser, semantic analysis, IR, ABI lowering
- NASM backend for `linux-x64-sysv` and `windows-x64-msabi`
- Runtime library with stdout helpers and runtime contract markers
- CLI: `build`, `emit-nasm`, `run`, `test`, `bench`, `explain-abi`, `generate-header`, `generate-pinvoke`, `doctor`
- MCP server with compile/build/run/test/interop tools
- 77 unit tests and 16 native integration samples

[Unreleased]: https://github.com/megaalive/hlax64/compare/v0.1.0-alpha...HEAD
[0.1.0-alpha]: https://github.com/megaalive/hlax64/releases/tag/v0.1.0-alpha
