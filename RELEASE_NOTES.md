# v0.2.1-alpha — Assembly Lab bundle & terminal

## Try it now (no install)

**[Open HlaX64 Playground](https://megaalive.github.io/hlax64/playground/index.html?example=argv)** — HlaX64 source, generated NASM, line notes, IR summary, AI prompts.

Examples: [argv](https://megaalive.github.io/hlax64/playground/index.html?example=argv) · [wc](https://megaalive.github.io/hlax64/playground/index.html?example=wc) · [hexdump](https://megaalive.github.io/hlax64/playground/index.html?example=hexdump) · [filemagic](https://megaalive.github.io/hlax64/playground/index.html?example=filemagic)

The browser playground uses **cached** `hla64 explain --json` output. For live compilation, use the Assembly Lab bundle or CLI archives below.

## Downloads

| Asset | Platform |
|-------|----------|
| `hla64-linux-x64.tar.gz` | Linux x64 CLI + MCP |
| `hla64-win-x64.zip` | Windows x64 CLI + MCP |
| `assembly-lab-linux-x64.tar.gz` | Assembly Lab bundle (Linux): UI + CLI + MCP + runtime + docs/examples |
| `assembly-lab-win-x64.zip` | Assembly Lab bundle (Windows): UI + CLI + MCP + runtime + docs/examples |
| `checksums.txt` | SHA-256 |
| `sbom-deps.json` | Dependency SBOM |

### Quick start (Assembly Lab bundle)

1. Extract `assembly-lab-win-x64.zip` (Windows) or `assembly-lab-linux-x64.tar.gz` (Linux).
2. Run `install.ps1` / `install.sh` (optional — adds shortcuts and PATH helpers).
3. Launch `HlaX64.AssemblyLab.exe` / `HlaX64.AssemblyLab`.
4. Open **Settings → Toolchain**, click **Test** — bundled CLI, runtime, and docs should resolve automatically.
5. Open the **Terminal** tab — interactive shell with Build/Run/Doctor injection.

CLI-only users: extract `hla64-*`, add to `PATH`, run `hla64 doctor`. Full guide: [docs/install.md](docs/install.md).

## What's new since v0.2.0-alpha

- **Just-work bundle** — single archive with UI, CLI, MCP, runtime NASM, examples, docs, wrapper scripts, and install helpers
- **Embedded terminal** — PTY shell with scroll-stable overlay caret; key repeat and prompt tracking fixes
- **Toolchain Settings** — browse/auto-detect/test/reset for NASM, linkers, runtime dir; shared resolver with `hla64 doctor`
- **Release smoke tests** — `scripts/smoke-assembly-lab-bundle.ps1` validates doctor, hello build/run, bundled paths
- **Conformance guardrails** — `verify-conformance` script in CI; hello NASM snapshot test; emitter drift documentation
- **Contributing** — issue backlog doc, walkthrough, `.editorconfig`, Doctor install hints

Full changelog: [CHANGELOG.md](CHANGELOG.md) · Compare: [v0.2.0-alpha...v0.2.1-alpha](https://github.com/megaalive/hlax64/compare/v0.2.0-alpha...v0.2.1-alpha)
