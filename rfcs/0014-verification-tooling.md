# RFC 0014 — Verification Tooling (Phase 18)

| Field | Value |
|-------|-------|
| **Status** | Implemented (Sprints 1–6) |
| **Phase** | 18 |
| **Since** | 0.1.0-alpha |

## Summary

Phase 18 adds static verification passes and CLI tooling to catch common low-level mistakes before assembly/link:

- **Definite assignment** (HLAX0060) — reading locals before initialization
- **CFG checks** (HLAX0061/62) — unreachable code, missing `@returns` assignment
- **Register liveness** (HLAX0063) — caller-saved registers live across `call`
- **`hla64 verify-stack`** — stack slot layout, prologue/epilogue, alignment
- **`hla64 verify-abi`** — per-procedure ABI report (params, return reg, externs)
- **Fuzz expansion** — lexer/formatter/manifest robustness tests

## CLI flags

| Flag | Diagnostics |
|------|-------------|
| `-Wdefinite` | HLAX0060 |
| `-Wunreachable` | HLAX0061, HLAX0062 |
| `-Wliveness` | HLAX0063 |
| `-Wverify` | all of the above |

LSP enables HLAX0060–63 by default (warnings).

## Commands

```bash
hla64 verify-stack examples/curriculum/01-arithmetic/add-two.hla64 [--target linux-x64-sysv] [--json]
hla64 verify-abi examples/curriculum/06-abi/stack-args-sysv.hla64 [--json]
```

## Stack verifier (HLAX0064–68)

`StackVerifier` inspects `IrFunction` layouts (`ProcedureStackMap`) and lowered NASM prologue/epilogue text:

- Non-overlapping stack slots
- `push rbp` / `mov rbp, rsp` on procedures
- Epilogue before fall-through return
- 16-byte frame alignment

## ABI verifier

Reuses `AbiArgumentClassifier` register assignment (SysV / Windows) and lists `extern procedure` symbols from the semantic registry.

## Deferred to Phase 19

**Differential testing MVP** (audit §5.28): compile small arithmetic samples and compare exit codes against known native binaries. Stub documented here; harness will live under `tests/differential/` when NASM/link CI is stable on all agents.

## References

- Audit Tier E §5.23–5.30
- [diagnostics.md](../docs/diagnostics.md) HLAX0060–69
- [development.md](../docs/development.md)
