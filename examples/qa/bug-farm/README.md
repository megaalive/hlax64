# Bug Farm (`examples/qa/bug-farm/`)

Stress cases whose primary job is to **surface compiler, ABI, and runtime bugs** — not to teach syntax. Real tools in `10-real-tools/` already caught Win64 stack alignment, CFG ordering, and bitwise lowering gaps.

## Cases (complete)

| Case | Stress | Manifest |
|------|--------|----------|
| `call-inside-loop` | callee-saved vs volatile across calls | `bugfarm-call-inside-loop` |
| `nested-while` | loop header/continuation layout | `bugfarm-nested-while` |
| `register-pressure` | live ranges across Win64 calls | `bugfarm-register-pressure` |
| `deep-nested-if` | CFG block explosion | `bugfarm-deep-nested-if` |
| `many-locals` | stack frame / spill paths | `bugfarm-many-locals` |
| `many-procedures` | symbol emission, call graph | `bugfarm-many-procedures` |
| `many-externs` | import table, call lowering | `bugfarm-many-externs` |
| `large-static-buffer` | BSS sizing, addressing | `bugfarm-large-static-buffer` |
| `many-stdout-args` | runtime call expansion | `bugfarm-many-stdout-args` |

Add one case per compiler fix so each regression has a minimal repro outside the full real-tool binaries.

## Running

Compile-only manifests live under `tests/examples-curriculum/bugfarm-*`.

```powershell
hla64 test tests/examples-curriculum --filter bugfarm- --compile-only
```
