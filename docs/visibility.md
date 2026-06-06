# Visibility & GitHub Pages

Resources for sharing HlaX64 with new contributors and users.

## GitHub Pages

Static docs under `docs/` deploy via `.github/workflows/pages.yml` on push to `main`.

After the first workflow run, enable **Settings → Pages → Source: GitHub Actions** if prompted.

Public URLs (once enabled):

| Path | Content |
|------|---------|
| `/playground/index.html` | Static syntax playground |
| `/install.md` | Installation guide (rendered as markdown on GitHub; link from README) |
| `/tutorials/` | Tutorial series |

Site base: `https://megaalive.github.io/hlax64/`

## Architecture diagram

See [architecture.md](architecture.md) for the Mermaid pipeline diagram (renders on GitHub and Pages).

## Demo recording (maintainers)

Regenerate the README GIF (uses real `hla64` CLI output):

```bash
python scripts/generate-demo-gif.py
```

Output: `docs/assets/demo.gif` (linked from README).

Manual recording alternative: [asciinema](https://asciinema.org/) + [agg](https://github.com/asciinema/agg).

## Release announcement checklist

When tagging `v0.1.0-alpha`:

- [ ] Verify Linux + Windows archives and checksums on Releases
- [ ] Update README install link to [install.md](install.md)
- [ ] Post short summary: compiler + MCP + 20 examples + `hla64 doctor`
- [ ] Tag issues with `good first issue` as they are created — see [open issues](https://github.com/megaalive/hlax64/issues?q=is%3Aissue+is%3Aopen)

## Repository metadata (GitHub UI)

Suggested description: *HLA-inspired x64 assembly compiler — NASM, CLI, MCP, Linux & Windows ABIs*

Topics: `compiler`, `assembly`, `x64`, `nasm`, `dotnet`, `mcp`, `hla`

Enable **Discussions** for Q&A and **Issues** templates (already in `.github/ISSUE_TEMPLATE/`).

## Logo

No official logo yet. A simple monospace `hla64` wordmark or x64 register icon is sufficient for v0.1.
