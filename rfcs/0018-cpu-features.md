# RFC 0018 — CPU Features (Phase 21)

| Field | Value |
|-------|-------|
| **Status** | MVP |
| **Phase** | 21 |

## Flags

```bash
hla64 build app.hla64 --cpu baseline-x64 --features +sse2,-avx2
```

## Validation

Unknown SIMD mnemonics for disabled features → **HLAX0070**.

Instruction metadata: `data/instructions.json`.
