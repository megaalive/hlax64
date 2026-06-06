# Changelog

All notable changes to HlaX64 are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/) after 1.0; during 0.x, breaking changes are allowed with documentation.

## [Unreleased]

### Added

- `hla64 explain` — IR, ABI lowering, and NASM for a source file (`--json`)
- `hla64 format` / `format --check` — source normalization
- VS Code extension skeleton under `editors/vscode/`
- `tests/conformance/` valid/invalid suites with diagnostic codes
- RFC index (`rfcs/`) with language versioning and pointer model drafts
- 16 structured examples across curriculum folders
- .NET global tool packaging (`dotnet tool install --global HlaX64.Cli`)

### Changed

- `CompilationResult.StructuredDiagnostics` for coded diagnostics in tooling

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
