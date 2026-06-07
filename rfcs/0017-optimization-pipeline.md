# RFC 0017 — Optimization Pipeline (Phase 20)

| Field | Value |
|-------|-------|
| **Status** | MVP (O1 + peephole) |
| **Phase** | 20 |

## Levels

| Flag | Behavior |
|------|----------|
| `--optimize O0` | Default — no IR optimization |
| `--optimize O1` | Constant folding on IR (`imm+imm` → `imm`) |
| `--optimize O2` | O1 + copy propagation through `mov` chains + aggressive peephole (`mov reg,0` → `xor reg,reg`) |

## Peephole

Post-lowering removal of `mov reg, reg` and `add reg, 0`.

## Register allocation

`--register-mode explicit` (default) — programmer owns registers.

`--register-mode assisted` — deferred; documented for Phase 20+ hardening.
