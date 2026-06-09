# Conformance tests

Each subdirectory under `valid/` or `invalid/` is one case:

| File | Purpose |
|------|---------|
| `source.hla64` | Input program |
| `manifest.json` | Expected compile outcome and optional output fragments |

`ConformanceTests` (in `tests/HlaX64.Compiler.Tests/ConformanceTests.cs`) discovers every case automatically.

## Valid cases

- **`expectNasmContains`** — each string must appear in emitted NASM (substring match, platform-stable fragments only).
- **`expectIrContains`** — each string must appear in lowered IR text.

Use short, stable fragments (`global _start`, `mov rax, qword [rcx]`, `; RUNTIME:`). Avoid absolute paths, line numbers, or labels that change with unrelated edits.

## Invalid cases

- **`expectCodes`** — diagnostic codes that must appear (e.g. `HLAX0001`).
- **`expectParseError`** — parser must throw.
- **`expectWarningsOnly`** — compile succeeds with warnings only.

## When you change code generation

If you edit **NASM emission** (`src/HlaX64.Backend.Nasm/`), **IR lowering**, or semantics that change emitted text:

1. Run `scripts/verify-conformance.ps1` (or `scripts/verify-conformance.sh` on Linux).
2. If a case fails, update its `manifest.json` **in the same PR** as the emitter change.
3. Do not merge emitter changes with stale manifests — CI runs conformance explicitly on every push and PR.

To inspect emitted NASM for one case:

```bash
dotnet run --project src/HlaX64.Cli -- emit-nasm tests/conformance/valid/<case>/source.hla64
```

## Adding a new case

1. Create `tests/conformance/valid/<kebab-name>/` or `invalid/<kebab-name>/`.
2. Add `source.hla64` and `manifest.json`.
3. Run `scripts/verify-conformance.ps1` locally before opening a PR.

See also [CONTRIBUTING.md](../../docs/rules/CONTRIBUTING.md) and issue templates for NASM snapshot tasks.
