# Curriculum example manifests

Native integration tests for structured `examples/` programs. Each manifest references a curriculum source file via a relative path.

```bash
hla64 test tests/examples-curriculum
hla64 test tests/examples-curriculum --filter curriculum-hello
hla64 test tests/examples-curriculum --filter real- --compile-only
```

29 runnable Linux curriculum examples are covered. The nine `real-*` manifests compile-check Windows-focused daily-use tools; their native Windows execution is locked by `RealTool_builds_and_runs_natively_on_windows` in `HlaX64.AssemblyLab.Tests`, which reads each tool's `expected.stdout`, `expected.exitcode`, and optional `expected.arguments`.

See [docs/tutorials/01-getting-started.md](../../docs/tutorials/01-getting-started.md).
