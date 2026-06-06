# RFC 0001 — Language Versioning

- **Status:** Accepted
- **Author:** HlaX64 maintainers

## Summary

HlaX64 uses explicit language and compiler version headers during the 0.x series, with documented breaking changes until a stable 1.0 release.

## Motivation

Contributors and AI agents need predictable compatibility rules before the language stabilizes.

## Design

- Language version in `docs/language-spec.md` header (currently **0.1**).
- Compiler version via `hlaX64 --version` (currently **0.1.0-alpha**).
- Policy in [docs/compatibility.md](../docs/compatibility.md):
  - Pre-1.0: breaking changes allowed with CHANGELOG + migration notes.
  - Post-1.0: semver, deprecation period, stable JSON schemas.

## Implementation

- [x] `docs/compatibility.md`
- [x] Language spec header
- [x] CHANGELOG.md
- [x] CLI JSON `schemaVersion`

## Unresolved

- Exact date for 1.0 criteria (test coverage, Windows parity, LSP).
