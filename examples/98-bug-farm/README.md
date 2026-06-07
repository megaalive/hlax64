# Bug Farm (`examples/98-bug-farm/`)

Stress cases whose primary job is to **surface compiler, ABI, and runtime bugs** — not to teach syntax. Real tools in `10-real-tools/` already caught Win64 stack alignment, CFG ordering, and bitwise lowering gaps.

## Planned cases

| Case | Stress |
|------|--------|
| `deep-nested-if` | CFG block explosion |
| `nested-while` | loop header/continuation layout |
| `many-locals` | stack frame / spill paths |
| `many-procedures` | symbol emission, call graph |
| `many-externs` | import table, call lowering |
| `large-static-buffer` | BSS sizing, addressing |
| `many-stdout-args` | runtime call expansion |
| `register-pressure` | live ranges across Win64 calls |
| `call-inside-loop` | callee-saved vs volatile across calls |

## Status

First cases landed:

| Case | Path |
|------|------|
| `call-inside-loop` | `call-inside-loop/` — compile-only manifest `bugfarm-call-inside-loop` |

Add one case per compiler fix so each regression has a minimal repro outside the full real-tool binaries.

## Running

Compile-only manifests will live under `tests/examples-curriculum/bugfarm-*` once cases land.
