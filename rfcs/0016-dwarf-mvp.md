# RFC 0016 — DWARF Line Info MVP (Phase 19)

| Field | Value |
|-------|-------|
| **Status** | Implemented (Linux stub) |
| **Phase** | 19 |

## Summary

When `--debug-info` is set on Linux targets, NASM emits `%line` directives and a `.debug_line` stub section.

## Windows

Deferred — PDB/CodeView integration planned post-MVP.
