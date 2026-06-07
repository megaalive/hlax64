# RFC 0015 — Source Maps (Phase 19)

| Field | Value |
|-------|-------|
| **Status** | Implemented (MVP) |
| **Phase** | 19 |

## Summary

`*.hlamap.json` sidecar maps source lines → IR instruction id → NASM line labels (best effort).

## CLI

```bash
hla64 build app.hla64 --source-map
hla64 emit-nasm app.hla64 -o app.nasm --source-map
```

## Format

JSON with `version`, `source`, `compilerVersion`, `entries[]` (`sourceLine`, `sourceColumn`, `irId`, `irOpcode`, `function`, `nasmLine`, `nasmLabel`).

## Hardening addendum

`SourceMapDocument` exposes `LookupBySource`, `LookupByNasmLine`, and `LookupByIrId` for round-trip lookup in tests and `hla64 disasm`.

Full bidirectional stepping, column ranges, inline expansion.
