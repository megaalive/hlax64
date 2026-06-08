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
  runner/euler-runner.hla64       # hla64 run … -- <n>
  problems/                       # one executable per problem
  expected/                       # reference answers (for tests)
  docs/patterns.md
  docs/performance-notes.md
  shared/README.md                # copy-paste helpers (no #include yet)
```

## Usage

Run a single problem (recommended):

```bash
hla64 run examples/20-project-euler/problems/euler001-multiples-of-3-and-5-bruteforce.hla64
hla64 run examples/20-project-euler/problems/euler001-multiples-of-3-and-5-formula.hla64
```

Runner (problems **1–3** today):

```bash
hla64 run examples/20-project-euler/runner/euler-runner.hla64 -- 1
hla64 run examples/20-project-euler/runner/euler-runner.hla64 -- 2
```

Windows MS ABI:

```bash
hla64 run examples/20-project-euler/runner/euler-runner.hla64 --target windows-x64-msabi -- 1
```

## Roadmap

| Phase | Problems | Status |
|-------|----------|--------|
| 1 | #1–#3 (+ #1 formula) | **started** |
| 2 | #4–#10 | planned |
| 3 | #11–#25 | planned |
| 4 | #26–#50 | planned |

Higher numbers: `*-template.hla64` stubs only (no published answers).

## Playground

After regenerating cache (`scripts/generate-playground-cache.ps1`), open:

- `docs/playground/index.html` → **Euler #1 (bruteforce)** / **Euler #1 (formula)**

Compare naive vs optimized NASM side by side.
