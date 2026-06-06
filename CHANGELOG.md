# Changelog

All notable changes to HlaX64 are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/) after 1.0; during 0.x, breaking changes are allowed with documentation.

## [Unreleased]

### Added

- **Phase 16 Sprint 5:** program-scope `static`/`endstatic` (`.data`/`.bss` globals); `cstring` type alias; built-in `utf8slice` record; HLAX0045–49; RFC 0007/0008; examples `global-counter.hla64`, `cstring-walk.hla64`
- **Phase 16 Sprint 5+:** `packed record`; procedure-scoped `enum`/`record`; enum auto-increment members (`Red := 1; Green; Blue;`)
- LSP grammar: `static`, `endstatic`, `cstring`, `utf8slice`, `packed`; formatter support
- **Phase 16 Sprint 3:** program-level `enum`/`endenum` with typed backing (`uint32`/`int32`/`uint64`/`int64`); qualified `Enum.Member` immediates; HLAX0039–41; RFC 0005; example `examples/02-types/color-enum.hla64`; conformance `color-enum`, `enum-duplicate-member`, `enum-undefined-member`
- **Phase 16 Sprint 4:** program-level `record`/`endrecord` with natural alignment; `var header: RecordType` stack blobs; dot field access; compile-time `sizeof`/`offsetof`; HLAX0042–44; RFC 0006; example `examples/02-types/patient-header.hla64`; conformance `patient-header`, `record-unknown-field`
- LSP grammar: `enum`, `endenum`, `record`, `endrecord`, `sizeof`, `offsetof`; `AstFormatter` support for enums and records
- **Phase 16 Sprint 2:** runtime `:=` assignment for int64 scalar locals and 64-bit registers; operators `+ - * / % & | ^ ~ << >>` and comparisons `== != < <= > >=`; IR + SysV/Win64 NASM lowering; HLAX0035–HLAX0038; example `examples/01-arithmetic/expr-assign.hla64`; conformance `expr-assign`, `expr-invalid-target`, `expr-divide-by-zero`
- Expression evaluation uses **`rax`/`rbx` as scratch** (documented in RFC 0004 and language spec)
- LSP grammar: `const`, `endconst`, `:=`, hex `$..`; formatter support for `:=` statements

### Added

- **Phase 16 Sprint 1:** compile-time `const` / `endconst` blocks with `:=` assignments and int64 expression evaluation (`+ - * / % & | ^ ~ << >>`, hex `$FF`); use in immediates and `type[ConstName]` array sizes (RFC 0004)
- Diagnostics HLAX0031–HLAX0034 for const errors; example `examples/05-memory/const-buffer-size.hla64`; conformance cases `const-expressions`, `const-divide-by-zero`
- `docs/memory-model.md` (cstring / utf8slice concepts, implemented vs planned); Phases 16–24 in `docs/roadmap.md`
- RFC [0004](rfcs/0004-expressions-and-constants.md) (const + runtime `:=` expressions implemented)

### Changed

- README: **Current Capabilities — HlaX64 0.1 Alpha** (replaces MVP Linux-only framing); CI-focused test badge; simplified project layout
- `docs/compiler-architecture.md`: Implemented / Experimental / Planned sections for compiler 0.1.x

### Added (prior unreleased)

- `-Wbounds` / `--warn-bounds` static array index warnings (HLAX0030); enabled in LSP diagnostics by default
- LSP go-to-definition, document symbols, and document formatting (`AstFormatter`); VS Code format-on-save for `.hla64`
- Packed stack arrays: `byte[N]`, `word[N]`, `dword[N]` with correct element stride and sized NASM loads/stores
- Example `array-byte-last.hla64`, conformance `array-byte-packed`, curriculum manifest (29 total)

### Added (prior unreleased)
- Four array examples (`array-sum`, `array-fill`, `array-max`, `array-literal-index`) plus native samples
- LSP Phase 14 MVP: hover, completion, parse-error positions; VS Code extension language client
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

- Docs sync: roadmap Fase 14, tutorial 05 (packed arrays, `-Wbounds`), examples catalog, development guide, GitHub Pages LSP section

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
