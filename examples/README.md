# Structured example programs for learning and smoke testing.

| Folder | Programs | Topic |
|--------|----------|-------|
| [00-getting-started/](00-getting-started/) | hello, exitcode | First programs |
| [01-arithmetic/](01-arithmetic/) | simple, move-values | Register arithmetic |
| [02-types/](02-types/) | signed-compare, unsigned-compare | Comparisons |
| [03-control-flow/](03-control-flow/) | count, if-else | Loops & branches |
| [04-procedures/](04-procedures/) | add-two, no-args, six-args | Procedures & ABI args |
| [05-memory/](05-memory/) | sum-1-to-5 | Accumulator patterns |
| [06-abi/](06-abi/) | stack-alignment, callee-saved | ABI conventions |
| [07-interop/](07-interop/) | export-lib | C / C# interop |

**16 programs** — clean-room HlaX64 code. See [docs/classic-hla-comparison.md](../docs/classic-hla-comparison.md).

```bash
hla64 run examples/00-getting-started/hello.hla64
hla64 explain examples/04-procedures/add-two.hla64
hla64 format examples --check
```
