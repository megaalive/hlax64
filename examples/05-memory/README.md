# Memory

Stack locals, address-of, and load through a pointer register (RFC 0002 minimal).

| Example | Description |
|---------|-------------|
| [sum-1-to-5.hla64](sum-1-to-5.hla64) | Accumulator loop 1..5 (register-only) |
| [pointer-load-store.hla64](pointer-load-store.hla64) | `&slot` + `mov([rcx], rax)` — exit 42 |

Full `var` stack slots without pointers: `tests/samples/local_var/`. Arrays and indexed access remain future work — see [rfcs/0002-pointer-model.md](../../rfcs/0002-pointer-model.md).
