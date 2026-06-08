# Project Euler suite (HlaX64)

Selected early [Project Euler](https://projecteuler.net/) problems implemented in HlaX64 — one source file per problem, plus a small argv runner.

## Ethics & licensing

Project Euler problem statements are **CC BY-NC-SA 4.0**. The community generally discourages publishing full solution write-ups for higher-numbered problems.

This directory focuses on:

- Low-level implementation patterns (loops, `mod`, primes, accumulation)
- Generated NASM inspection (`hla64 explain`, playground)
- Reusable math snippets in [`docs/patterns.md`](docs/patterns.md)

**Solve the problems yourself first.** Use these files to learn HlaX64 and assembly lowering — not to copy answers.

## Layout

```
examples/project-euler/
  README.md
  runner/euler-runner.hla64       # hla64 run … -- <n>  (ParseU64, 1–25)
  problems/                       # one executable per problem
  expected/                       # reference answers (euler001.txt … euler025.txt)
  data/                           # grid/digit inputs for generators
  docs/patterns.md
  docs/performance-notes.md
  shared/README.md                # copy-paste helpers (no #include yet)
```

Generators live in repo `scripts/` (`generate_euler011.py`, `generate_euler013.py`, `generate_euler022.py`, …).

## Usage

Run a single problem (recommended):

```bash
hla64 run examples/project-euler/problems/euler004-largest-palindrome-product.hla64
hla64 run examples/project-euler/problems/euler010-sum-primes-below-2m.hla64
```

Runner (problems **1–3** inline; **4–25** points to `problems/`):

```bash
hla64 run examples/project-euler/runner/euler-runner.hla64 -- 1
hla64 run examples/project-euler/runner/euler-runner.hla64 -- 15
```

Windows MS ABI:

```bash
hla64 run examples/project-euler/runner/euler-runner.hla64 --target windows-x64-msabi -- 1
```

Regenerate data-heavy sources:

```powershell
powershell -File scripts/generate-euler008.ps1
python scripts/generate_euler011.py
python scripts/generate_euler013.py
python scripts/generate_euler022.py
```

## Roadmap

| Phase | Problems | Status |
|-------|----------|--------|
| 1 | #1–#3 (+ #1 formula) | **done** |
| 2 | #4–#10 | **done** |
| 3 | #11–#25 | **done** (all verified) |
| 4 | #26–#50 | **stubs** (`eulerNNN-template.hla64`) |

### Implementation notes

| Problem | Style |
|---------|--------|
| #1–#10, #12–#25 | Full HlaX64 solvers (verified via WSL run) |
| #11, #13, #16–#18, #20, #22 | Grid scanner, bignum sum/add, name scores, DP |
| #24 | Lehmer used-digit scan; runtime `idiv`/`mod` decomposition (see `patterns.md`) |
| #8 | 1000-digit string via `scripts/generate-euler008.ps1` |
| #13 | 100×50-digit bignum add → first 10 digits; `scripts/generate_euler013.py` |
| #22 | Sorted name letter scores × rank; `scripts/generate_euler022.py` embeds `data/euler022-names.txt` |
| #26–#50 | Stubs only |

Generated solvers: `python scripts/generate_euler013.py` (checksum `5537376230`), `python scripts/generate_euler022.py` (checksum `871198282`). Both download Project Euler resources if `data/` is missing.

## Playground

After regenerating cache (`scripts/generate-playground-cache.ps1`), open:

- `docs/playground/index.html` → **Euler #1 (bruteforce)** / **Euler #1 (formula)**

Compare naive vs optimized NASM side by side.
