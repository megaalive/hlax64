# RFC 0019 — SIMD Intrinsics

| Field | Value |
|-------|-------|
| **Status** | Partially implemented |
| **Phase** | 21 |

## MVP intrinsics (AVX2 required)

| Intrinsic | Lowers to | Args |
|-----------|-----------|------|
| `simd.add_f64x4(a, b)` | `vaddpd a, b` | 2 YMM regs |
| `simd.load_f64x4(ptr)` | `vmovapd ymm0, [ptr]` | 1 ptr; optional dest reg |
| `simd.store_f64x4(ptr, val)` | `vmovapd [ptr], val` | ptr, YMM reg |

Also supported: inline asm mnemonics `vaddpd`, `vmovapd`, `vxorpd` when `--features +avx2`.

## Diagnostics

- **HLAX0070** — AVX2 feature required
- **HLAX0072** — wrong intrinsic arity

## Deferred

- Typed `f64x4` values, constant propagation, Windows-specific ymm save/restore
