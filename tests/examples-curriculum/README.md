# Curriculum example manifests

Native integration tests for structured `examples/` programs. Each manifest references a curriculum source file via a relative path.

```bash
hla64 test tests/examples-curriculum
hla64 test tests/examples-curriculum --filter curriculum-hello
```

19 runnable Linux examples are covered (Windows-only and shared-library examples use dedicated docs).

See [docs/tutorials/01-getting-started.md](../../docs/tutorials/01-getting-started.md).
