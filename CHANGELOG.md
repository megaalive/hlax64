# Changelog

All notable changes to HlaX64 are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/) after 1.0; during 0.x, breaking changes are allowed with documentation.

## [Unreleased]

### Added

- MCP tools: `explain`, `format-source`, `doctor`
- Shared `ExplainReport` / `DoctorReport` services used by CLI and MCP
- 4 curriculum examples (20 total `.hla64` files)
- Conformance: NASM/IR substring checks, unknown-type cases
- `docs/mcp-tools.md`, `docs/playground-design.md`, static `docs/playground/index.html`
- `ExamplesCompileTests` — CI guard that all 20 examples compile end-to-end
- `AstFormatter` (parse → re-emit) used by `hla64 format`
- `HlaX64.LanguageServer` diagnostics MVP (stdio LSP)
- `DiagnosticService` for frontend-only analysis
- Parameter type validation (`HLAX0020`) and parse diagnostic `HLAX1000`

### Changed

- `DoctorCommand` delegates to `DoctorReport.Run()`
- `add-two` example includes `stdlib64.hhf` for `stdout.put`

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
