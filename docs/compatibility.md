# HlaX64 Compatibility Policy

> **Language version:** 0.1 · **Compiler:** HlaX64 0.x · **Status:** Pre-1.0

## Before v1.0 (current)

During the 0.x series:

- **Breaking changes are allowed** when they improve correctness, ABI safety, or long-term design.
- Every breaking change **must be documented** in [CHANGELOG.md](../CHANGELOG.md) and the PR description.
- A **migration note** is required when user source or CLI flags change.
- The **language version** in [language-spec.md](language-spec.md) must be incremented for language-breaking changes.
- **Tests and examples** must be updated in the same change.

## After v1.0 (planned)

Once HlaX64 reaches 1.0:

- **Semantic versioning** for releases.
- **Deprecation before removal** — at least one minor release with warnings.
- **Stable CLI flags** and **JSON schemas** within a major version (`schemaVersion` bumps only on breaking JSON changes).
- **Stable runtime ABI** for exported runtime symbols within a major version.

## What counts as breaking

| Change | Breaking? |
|--------|-----------|
| New keyword or optional syntax | No (additive) |
| Changed meaning of existing syntax | Yes |
| Removed or renamed CLI flag | Yes |
| Changed default target or runtime mode | Yes |
| Changed JSON field meaning | Yes (bump `schemaVersion`) |
| New diagnostic for invalid code | No |
| Stricter validation of previously accepted invalid code | Yes (document migration) |

## Version sources

| Artifact | Location |
|----------|----------|
| Compiler version | `hla64 --version` → `Compilation.GetVersion()` |
| Language version | Header of [language-spec.md](language-spec.md) |
| JSON schema version | `schemaVersion` field in CLI JSON output |
| Runtime contract | `HLAX64-RUNTIME-FUNCTION v0.1` markers |

## Reporting compatibility issues

Open a [Bug Report](https://github.com/megaalive/hlax64/issues/new?template=bug_report.yml) if a documented guarantee is violated, or a [Language Proposal](https://github.com/megaalive/hlax64/issues/new?template=language_proposal.yml) for intentional breaking changes.
