# Curriculum example manifests

Native integration tests for structured `examples/` programs. Each manifest references a curriculum source file via a relative path.

```bash
hla64 test tests/examples-curriculum
hla64 test tests/examples-curriculum --filter curriculum-hello
hla64 test tests/examples-curriculum --filter real- --compile-only
```

29 runnable Linux curriculum examples are covered. The nine `real-*` manifests compile-check Windows-focused daily-use tools; `interop-*` manifests compile-check shared-library exports. Native Windows execution for real-tools and C# interop callers is locked by `AssemblyLabBackendTests`.

See [docs/tutorials/01-getting-started.md](../../docs/tutorials/01-getting-started.md).
