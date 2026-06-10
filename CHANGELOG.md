# Changelog

All notable changes to HlaX64 are documented here.

Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/). Versioning follows [Semantic Versioning](https://semver.org/) after 1.0; during 0.x, breaking changes are allowed with documentation.

## [Unreleased]

## [0.2.2-alpha] - 2026-06-10

**Assembly Lab Win32 debug** — native stepping without MinGW GDB, HLA line mapping, debug args prompt, keyboard shortcuts.

### Added

- Win32 debug API backend (`Win32DebugBackend`) with instruction Step Over/Into/Out, call-site line mapping, and shutdown auto-continue
- Assembly Lab debug args dialog, F5/F10/F11/Shift+F11 shortcuts, DAP user-action logging
- `tests/HlaX64.DebugAdapter.Tests` (21 tests)

### Fixed

- Linux `sys.nasm` assemble on CI NASM (`mul` vs `imul`, SysV register widths)
- Windows `netcheck` calls `hlax_net_init()` before TCP connect (parity with Linux)

## [0.2.1-alpha] - 2026-06-08

**Assembly Lab bundle & terminal polish** — installable just-work archives, embedded PTY terminal, toolchain settings, conformance drift guards.

### Highlights

- **Assembly Lab bundle** — `assembly-lab-win-x64.zip` / `assembly-lab-linux-x64.tar.gz` with CLI, MCP, runtime, docs, examples, `install.ps1` / `install.sh`, and `bundle-manifest.json`
- **Embedded terminal** — interactive PTY (PowerShell/cmd or bash/zsh) with overlay caret that tracks the prompt on scroll; Build/Run/Doctor command injection
- **Toolchain Settings** — persistent NASM/linker/runtime paths shared with `hla64 doctor`; bundled path resolution for app-local installs
- **Conformance** — `hello-snapshot` case (PR #23); `scripts/verify-conformance.ps1/.sh` in CI; clearer drift errors in `ConformanceTests`
- **Issue triage** — `docs/issue-backlog.md`, contributing walkthrough, Doctor remediation hints

### Fixed

- Test paths aligned with `examples/curriculum/` and `examples/tools/` layout
- `ToolchainResolverTests` isolates `HLAX64_RUNTIME_DIR` on Linux CI
- Stack and parameter memory operands emit explicit `qword`/`dword`/… sizes for strict NASM on Linux
- GNU `link` on Linux PATH no longer mistaken for MSVC Windows linker

## [0.2.0-alpha] - 2026-06-08

**Useful Assembly Tools & Onboarding** — GitHub Pages playground, real-tool examples, Phases 15–24, 400+ tests.

### Highlights

- **[Playground](https://megaalive.github.io/hlax64/playground/)** — 12 curated examples, cached generated NASM, explain-this-line tutor, AI debug/explain/optimize prompts, `?example=wc` deep links, Run locally panel
- **Real tools** — `wc`, `hexdump`, `filemagic`, `linecount`, `exists`, `fnv1a`, … (Windows + Linux ports); argv runtime (`hlax_argv_*`)
- **Assembly Lab** — Avalonia desktop app: live IR/NASM/ABI, build/run, DAP, Explain/Agent, proof bundle ([RFC 0024](rfcs/0024-assembly-lab.md))
- **Language core (Phase 16)** — `const`, runtime `:=`, `enum`, `record`/`struct`, `static`, `cstring`/`utf8slice`, `idiv`/`div`/`jmp`
- **ABI & FFI (Phase 17)** — stack args, `extern`, indirect calls, float/record params, variadic extern
- **Verification (Phase 18)** — HLAX0060–63, `verify-stack` / `verify-abi`, `-Wverify`
- **Runtime I/O** — `stdout.putu`, `stdout_put_uint`, `uint_to_str`
- **Docs** — `language-spec.md` (stdlib, UB, `#pragma target` planned), roadmap cleanup, README playground hero

### Added

- Phases 15–24: Assembly Lab, debug/explain (`--source-map`, `disasm`, `--trace`), `--optimize O1/O2`, instruction DB + AVX2 intrinsics, `hla64.toml`/restore, proof bundle, DAP server, MCP explain + `suggestedFix`
- LSP hardening: signature help, semantic tokens, format-on-save, go-to-definition, document symbols
- `examples/tools/10-windows/` and `examples/tools/12-linux/` with curriculum regression
- `examples/qa/bug-farm/`, `examples/qa/invalid/` catalogs
- C# interop examples: `native_count_lines`, `native_fnv1a`, `native_sum_bytes`
- `scripts/generate-playground-cache.ps1`; `docs/playground/cache/` explain JSON
- Diagnostics HLAX0030–0073 (bounds, const, enum, record, static, verification, SIMD, atomics)

### Changed

- README leads with **Try it now** playground CTA; clarifies cached compile vs live CLI
- Release archives include **Assembly Lab** bundles (`assembly-lab-linux-x64.tar.gz`, `assembly-lab-win-x64.zip`)

### Fixed

- Linux SysV: extern `addr:` operands, `hlax_argv_get` register, `stdout.put` multi-register save order
- Parser: `int64` minimum literal; SysV exit epilogue when `mov` targets `rbx`

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

[Unreleased]: https://github.com/megaalive/hlax64/compare/v0.2.1-alpha...HEAD
[0.2.1-alpha]: https://github.com/megaalive/hlax64/releases/tag/v0.2.1-alpha
[0.2.0-alpha]: https://github.com/megaalive/hlax64/releases/tag/v0.2.0-alpha
[0.1.0-alpha]: https://github.com/megaalive/hlax64/releases/tag/v0.1.0-alpha
