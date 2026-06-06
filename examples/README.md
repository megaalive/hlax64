# HlaX64 Examples

Structured sample programs for learning and smoke testing. Each folder has a short README.

```bash
hla64 run examples/00-getting-started/hello.hla64
dotnet run --project src/HlaX64.Cli -- test tests/samples
```

## Curriculum

| Folder | Topic | Programs |
|--------|-------|----------|
| [00-getting-started/](00-getting-started/) | Hello world, exit codes | `hello`, `exitcode` |
| [01-arithmetic/](01-arithmetic/) | Register arithmetic | `simple` |
| [03-control-flow/](03-control-flow/) | Loops and branches | `count`, `if-else` |
| [04-procedures/](04-procedures/) | Procedures & ABI | `add-two` |

More integration tests live under [`tests/samples/`](../tests/samples/). See [docs/examples.md](../docs/examples.md) and [docs/classic-hla-comparison.md](../docs/classic-hla-comparison.md).

## Run any example

```bash
hla64 run examples/<folder>/<name>.hla64
hla64 emit-nasm examples/00-getting-started/hello.hla64
hla64 explain-abi --target linux-x64-sysv
```

Examples are **clean-room** HlaX64 code (see [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md)).
