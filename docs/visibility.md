# Visibility & GitHub Pages

Resources for sharing HlaX64 with new contributors and users.

## GitHub Pages

Static docs under `docs/` deploy from the **`main`** branch (**Settings → Pages → Deploy from a branch → `/docs`**).

| Path | Content |
|------|---------|
| `/` | Landing page ([index.html](index.html)) |
| `/playground/index.html` | Static syntax playground |
| `/install.md` | Installation guide (raw markdown) |
| `/tutorials/` | Tutorial series (incl. Level 3 memory) |

Site base: `https://megaalive.github.io/hlax64/` — root URL serves the landing page; use `/playground/index.html` for the editor demo.

## Architecture diagram

See [architecture.md](architecture.md) for the Mermaid pipeline diagram (renders on GitHub and Pages).

## Release announcement checklist

Current tags: **`v0.2.0-alpha`** (latest pre-release), **`v0.1.0-alpha`** (pre-release, superseded).

When tagging a new **`v*.*.*-alpha`** release:

- [ ] Update [CHANGELOG.md](../CHANGELOG.md) and [RELEASE_NOTES.md](../RELEASE_NOTES.md)
- [ ] Bump `<Version>` in `src/HlaX64.Cli/HlaX64.Cli.csproj`
- [ ] Push annotated tag `vX.Y.Z-alpha` (triggers [.github/workflows/release.yml](../.github/workflows/release.yml))
- [ ] Verify Linux + Windows CLI + Assembly Lab archives and `checksums.txt` on [Releases](https://github.com/megaalive/hlax64/releases)
- [ ] Confirm both alpha tags show **Pre-release**; newest alpha is **Latest** ([normalize-releases.yml](../.github/workflows/normalize-releases.yml) runs on `main`)
- [ ] Update README playground / install links if needed

## Repository metadata (GitHub UI)

Suggested description: *HLA-inspired x64 assembly compiler — NASM, CLI, MCP, Linux & Windows ABIs*

Topics: `compiler`, `assembly`, `x64`, `nasm`, `dotnet`, `mcp`, `hla`

Enable **Discussions** for Q&A and **Issues** templates (already in `.github/ISSUE_TEMPLATE/`).

## Logo

No official logo yet. A simple monospace `hla64` wordmark or x64 register icon is sufficient for v0.1.
