# Issue backlog (triage)

Last reviewed: 2026-06-09. Use this as the canonical status when GitHub labels lag behind the repo.

## Closed (done in repo)

| Issue | Title | Resolution |
|-------|-------|------------|
| [#2](https://github.com/megaalive/hlax64/issues/2) | uint64 boundary integration test | Merged |
| [#8](https://github.com/megaalive/hlax64/issues/8) | hello NASM conformance snapshot | PR #23 + `tests/conformance/valid/hello-snapshot/` |
| [#9](https://github.com/megaalive/hlax64/issues/9) | benchmarks README | `examples/benchmarks/README.md` + JSON schema link |

## Closed by maintenance (2026-06-09)

| Issue | Title | Why closed |
|-------|-------|------------|
| [#5](https://github.com/megaalive/hlax64/issues/5) | Doctor when NASM missing | `installHint` + `remediation` in JSON; links `docs/install.md` |
| [#6](https://github.com/megaalive/hlax64/issues/6) | VS Code LSP in README | `editors/vscode/README.md` + main README editor section |
| [#7](https://github.com/megaalive/hlax64/issues/7) | CONTRIBUTING walkthrough | `docs/contributing-walkthrough.md` |
| [#10](https://github.com/megaalive/hlax64/issues/10) | `.editorconfig` for `.hla64` | Root `.editorconfig` |
| [#14](https://github.com/megaalive/hlax64/issues/14) | LSP hover/completion MVP | `HlaX64.LanguageServer` + `LanguageServerEditorServicesTests` |
| [#3](https://github.com/megaalive/hlax64/issues/3) | `install-linux.sh` | `scripts/install-linux.sh` + `docs/install.md` |
| [#4](https://github.com/megaalive/hlax64/issues/4) | `install-windows.ps1` | `scripts/install-windows.ps1` + `docs/install.md` |

## Open — still relevant

| Issue | Title | Notes |
|-------|-------|-------|
| [#1](https://github.com/megaalive/hlax64/issues/1) | Worked examples per HLAX code | `docs/diagnostics.md` has catalog; needs example snippet per code |
| [#11](https://github.com/megaalive/hlax64/issues/11) | Rust FFI sample | No `examples/` Rust interop sample yet |
| [#12](https://github.com/megaalive/hlax64/issues/12) | DWARF debug RFC | Design only; Windows DAP still limited |
| [#13](https://github.com/megaalive/hlax64/issues/13) | Playground live explain API | Playground uses cached artifacts only |
| [#15](https://github.com/megaalive/hlax64/issues/15) | Curriculum native tests on Windows CI | CI runs curriculum on Linux only; Windows has single exitcode smoke |
| [#16](https://github.com/megaalive/hlax64/issues/16) | Benchmark HTML/CSV exporter | `hla64 bench --json` only |
| [#17](https://github.com/megaalive/hlax64/issues/17) | MCP path audit + tests | `docs/mcp-security.md` exists; no automated path-rejection tests |
| [#18](https://github.com/megaalive/hlax64/issues/18) | Register clobber warnings | HLAX0063 covers call liveness; runtime-contract-driven warnings not done |
| [#19](https://github.com/megaalive/hlax64/issues/19) | Formatter nested if/while | `AstFormatterTests` minimal; needs conformance round-trip cases |
| [#20](https://github.com/megaalive/hlax64/issues/20) | GitHub Discussions FAQ | Community task; not started in repo |

## How to update this file

When closing an issue, move its row to **Closed** with a one-line resolution. When scope changes, edit **Open** notes instead of opening duplicate issues.
