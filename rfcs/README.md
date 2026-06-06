# RFC Process

HlaX64 uses lightweight RFCs for language design, ABI changes, and major tooling decisions.

## When to write an RFC

- New syntax or semantics
- Breaking changes
- New ABI or target support
- Major CLI or MCP contract changes

## Workflow

1. Copy `rfcs/template.md` to `rfcs/NNNN-short-title.md`
2. Fill in all sections
3. Open a GitHub Discussion or Language Proposal issue linking the RFC
4. After approval, implement with tests, spec updates, and CHANGELOG entry

## Index

| RFC | Title | Status |
|-----|-------|--------|
| [0001](0001-language-versioning.md) | Language versioning | Accepted |
| [0002](0002-pointer-model.md) | Pointer & memory ops | Partially implemented |
| [0003](0003-array-model.md) | Stack arrays `type[N]`, `arr[i]` | Implemented (0.x) |
| [0004](0004-expressions-and-constants.md) | Const blocks & expressions | Implemented (Sprint 1–2) |
| [0005](0005-enum-model.md) | Enum model | Implemented (Sprint 3) |
| [0006](0006-struct-layout.md) | Record layout, sizeof, offsetof | Implemented (Sprint 4) |
