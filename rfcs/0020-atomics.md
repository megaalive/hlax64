# RFC 0020 — Atomics

| Field | Value |
|-------|-------|
| **Status** | Partially implemented |
| **Phase** | 21 |

## MVP intrinsics

| Intrinsic | Lowers to |
|-----------|-----------|
| `atomic.load(ptr, ordering)` | `mov` + fences |
| `atomic.store(ptr, value, ordering)` | `mov` + fences |
| `atomic.fetch_add(ptr, delta, ordering)` | `lock xadd` + fences |

Orderings: `relaxed`, `acquire`, `release`, `acq_rel`, `seq_cst`.

Result of load/fetch_add is in `rax`.

## Diagnostics

- **HLAX0073** — invalid atomic call or ordering

## Deferred

- `cmpxchg`, typed atomic pointers, C++11 memory model verification
