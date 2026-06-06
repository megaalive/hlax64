# RFC 0021 — Packages and Manifest (Phase 22)

| Field | Value |
|-------|-------|
| **Status** | MVP |
| **Phase** | 22 |

## Manifest

`hla64.toml` — `name`, `version`, `target`, `sources`, `dependencies[]`.

Schema: `schemas/hla64.toml.schema.json`.

## Commands

- `hla64 new console [name]` — template project
- `hla64 restore` — reads manifest (dependency resolution stub)

## Lock file

`hla64.lock` — documented schema; full resolver deferred.
