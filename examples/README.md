# Structured example programs (20 clean-room `.hla64` files).

| Folder | Programs |
|--------|----------|
| [00-getting-started/](00-getting-started/) | hello, exitcode |
| [01-arithmetic/](01-arithmetic/) | simple, move-values, subtract, inc-dec |
| [02-types/](02-types/) | signed-compare, unsigned-compare |
| [03-control-flow/](03-control-flow/) | count, if-else |
| [04-procedures/](04-procedures/) | add-two, no-args, six-args, factorial-iter |
| [05-memory/](05-memory/) | sum-1-to-5 |
| [06-abi/](06-abi/) | stack-alignment, callee-saved, windows-exitcode |
| [07-interop/](07-interop/) | export-lib |
| [08-ai-agent/](08-ai-agent/) | smoke-test |
| [09-benchmarks/](09-benchmarks/) | README → `benchmarks/count.json` |

```bash
hla64 explain examples/08-ai-agent/smoke-test.hla64 --json
hla64 build examples/06-abi/windows-exitcode.hla64 --target windows-x64-msabi -o build/win-exit
```

See [docs/examples.md](../docs/examples.md).
