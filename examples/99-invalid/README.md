# Invalid Examples (`examples/99-invalid/`)

Educational sources that **must fail to compile** with a specific diagnostic code. These complement the automated conformance suite under `tests/conformance/invalid/`.

## Purpose

- Show learners what *not* to do
- Lock expected error codes so the compiler keeps producing actionable diagnostics
- Give agents a catalog of failure modes (bad ABI, bad types, missing return, etc.)

## Layout (planned)

Each entry follows:

```
bad-example-name/
├── bad-example-name.hla64
├── README.md              # why it should fail
└── expected-codes.txt     # e.g. HLAX0062
```

## Status

| Example | Code | Notes |
|---------|------|-------|
| `address-of-register` | HLAX0023 | `mov(&rax, …)` |
| `missing-return` | HLAX0062 | verification warning on empty `@returns` body |
| `unknown-instruction` | HLAX0071 | `movz` is not a HlaX64 instruction |
| `unknown-register` | HLAX0012 | `raxz` is not a valid register |

Automated: `ExamplesInvalidTests` reads each folder's `manifest.json` + `expected-codes.txt`.

Run conformance invalid tests today:

```powershell
hla64 test tests/conformance/invalid
```
