# Structured example programs

Examples are grouped by **category** at the top level. Each category keeps the numbered curriculum folders from the original layout.

```
examples/
  curriculum/          # Language learning path (00–08)
  interop/             # FFI, export-lib, C# P/Invoke
  tools/               # Real CLI tools (Windows + Linux)
  project-euler/       # PE #1–#50 suite
  benchmarks/          # → benchmarks/count.json
  qa/                  # bug-farm + invalid compile catalog
```

| Category | Folders | Programs |
|----------|---------|----------|
| [curriculum/](curriculum/) | `00-getting-started` … `06-abi`, `08-ai-agent` | hello, arithmetic, control flow, procedures, memory, ABI |
| [interop/](interop/) | `07-interop`, `11-csharp-interop-real` | extern puts, native DLL + C# callers |
| [tools/](tools/) | `10-windows`, `12-linux` | listfiles, wc, hexdump, fnv1a, … |
| [project-euler/](project-euler/) | problems, runner, data, expected | PE #1–#25 verified, #26–#50 stubs |
| [benchmarks/](benchmarks/) | README | Links to `benchmarks/count.json` |
| [qa/](qa/) | `bug-farm`, `invalid` | Stress cases + must-not-compile catalog |

```bash
hla64 explain examples/curriculum/08-ai-agent/smoke-test.hla64 --json
hla64 build examples/curriculum/06-abi/windows-exitcode.hla64 --target windows-x64-msabi -o build/win-exit
hla64 test tests/examples-curriculum --filter real- --compile-only
hla64 run examples/project-euler/problems/euler013-large-sum.hla64
```

**Playground:** [docs/playground/index.html](../docs/playground/index.html) — sidebar grouped by category; cache under `docs/playground/cache/<category>/`.

See [docs/examples.md](../docs/examples.md).
