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
20-project-euler/
  README.md
  runner/euler-runner.hla64       # hla64 run … -- <n>  (ParseU64, 1–25)
  problems/                       # one executable per problem
  expected/                       # reference answers (euler001.txt … euler025.txt)
  data/                           # grid/digit inputs for generators
  docs/patterns.md
  docs/performance-notes.md
  shared/README.md                # copy-paste helpers (no #include yet)
  scripts/generate_euler008.ps1   # embed PE #8 digit string
  scripts/generate_euler011.py    # grid scanner (optional regen)
```

## Usage

Run a single problem (recommended):

```bash
hla64 run examples/20-project-euler/problems/euler004-largest-palindrome-product.hla64
hla64 run examples/20-project-euler/problems/euler010-sum-primes-below-2m.hla64
```

Runner (problems **1–3** inline; **4–25** points to `problems/`):

```bash
hla64 run examples/20-project-euler/runner/euler-runner.hla64 -- 1
hla64 run examples/20-project-euler/runner/euler-runner.hla64 -- 15
```

Windows MS ABI:

```bash
hla64 run examples/20-project-euler/runner/euler-runner.hla64 --target windows-x64-msabi -- 1
```

Regenerate data-heavy sources:

```powershell
powershell -File scripts/generate-euler008.ps1
python scripts/generate_euler011.py
```

## Roadmap

| Phase | Problems | Status |
|-------|----------|--------|
| 1 | #1–#3 (+ #1 formula) | **done** |
| 2 | #4–#10 | **done** |
| 3 | #11–#25 | **done** (full impl #11/#16–#18/#20/#23; #24 Lehmer; templates #13/#22/#25) |
| 4 | #26–#50 | **stubs** (`eulerNNN-template.hla64`) |

### Implementation notes

| Problem | Style |
|---------|--------|
| #1–#10, #12, #14–#15, #19–#21, #25 | Full HlaX64 solvers (verified via WSL run) |
| #11, #16–#18, #20, #23 | Full solvers: grid scanner, bignum, DP, letter counts, inline divsum |
| #24 | Lehmer permutation (algorithm in source; verify output vs `expected/euler024.txt`) |
| #8 | 1000-digit string via `scripts/generate-euler008.ps1` |
| #13, #22 | Templates — need PE resource files embedded in `data/` (see below) |
| #26–#50 | Stubs only |

## Playground

After regenerating cache (`scripts/generate-playground-cache.ps1`), open:

- `docs/playground/index.html` → **Euler #1 (bruteforce)** / **Euler #1 (formula)**

Compare naive vs optimized NASM side by side.
