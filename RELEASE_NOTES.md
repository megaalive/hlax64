# v0.2.0-alpha — Useful Assembly Tools & Onboarding

## Try it now (no install)

**[Open HlaX64 Playground](https://megaalive.github.io/hlax64/playground/index.html?example=argv)** — HlaX64 source, generated NASM, line notes, IR summary, AI prompts.

Examples: [argv](https://megaalive.github.io/hlax64/playground/index.html?example=argv) · [wc](https://megaalive.github.io/hlax64/playground/index.html?example=wc) · [hexdump](https://megaalive.github.io/hlax64/playground/index.html?example=hexdump) · [filemagic](https://megaalive.github.io/hlax64/playground/index.html?example=filemagic)

The browser playground uses **cached** `hla64 explain --json` output. For live compilation, use the CLI archives below.

## Downloads

| Asset | Platform |
|-------|----------|
| `hla64-linux-x64.tar.gz` | Linux x64 CLI + MCP |
| `hla64-win-x64.zip` | Windows x64 CLI + MCP |
| `assembly-lab-linux-x64.tar.gz` | Assembly Lab (Linux) |
| `assembly-lab-win-x64.zip` | Assembly Lab (Windows) |
| `checksums.txt` | SHA-256 |
| `sbom-deps.json` | Dependency SBOM |

Extract, add to `PATH`, run `hla64 doctor`. See [docs/install.md](docs/install.md).

## What's new since v0.1.0-alpha

- **GitHub Pages playground** — 12 examples, NASM pane, Run locally commands, AI prompt copy
- **Real tools** — wc, hexdump, filemagic, linecount, exists, fnv1a (+ Linux ports); argv runtime
- **Assembly Lab** — desktop IR/NASM/ABI, build/run, DAP, Explain/Agent ([tutorial](docs/tutorials/06-assembly-lab.md))
- **Language** — const, `:=`, enum, record/struct, static, idiv/div/jmp, `stdout.putu`
- **Phases 17–18** — extern/FFI, verification warnings, `verify-stack` / `verify-abi`
- **400+ tests** — curriculum, bug-farm, invalid catalog, real-tools regression

Full changelog: [CHANGELOG.md](CHANGELOG.md) · Compare: [v0.1.0-alpha...v0.2.0-alpha](https://github.com/megaalive/hlax64/compare/v0.1.0-alpha...v0.2.0-alpha)
