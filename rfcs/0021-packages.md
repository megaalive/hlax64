# RFC 0021 — Packages and Manifest (Phase 22)

| Field | Value |
|-------|-------|
| **Status** | Partially implemented |
| **Phase** | 22 |

## Manifest

`hla64.toml` — `name`, `version`, `target`, `sources`, `[dependencies]`.

```toml
[dependencies]
helpers = { path = "../helpers" }
# remote = { git = "https://...", rev = "abc123" }
```

## Commands

- `hla64 restore` — resolves path/git deps, writes `hla64.lock`
- `hla64 build` — compiles main + dependency sources; fails on lock mismatch

## Lock file

`hla64.lock` — pinned name, rev/version, content hash, resolved path, source list.

## Deferred

- Version ranges, transitive dependency resolution, NuGet-style feeds
