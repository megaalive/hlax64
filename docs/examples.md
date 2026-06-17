# HlaX64 — Examples & How to Run

> **Direktori**: [`examples/`](../examples) (structured curriculum) ·
> [`tests/samples/`](../tests/samples) (integration tests with manifests).

---

## 1. Structured examples (`examples/`)

Top-level categories:

| Category | Path | Contents |
|----------|------|----------|
| **Curriculum** | [`curriculum/`](../examples/curriculum/) | `00-getting-started` … `06-abi`, `08-ai-agent` |
| **Interop** | [`interop/`](../examples/interop/) | `07-interop`, `11-csharp-interop-real` |
| **Tools** | [`tools/`](../examples/tools/) | `10-windows`, `12-linux` — use `hlax_file_*`, `hlax_stdout_write`, `hlax_path_exists`, `hlax_is_space`, `hlax_is_printable` stdlib helpers |
| **Project Euler** | [`project-euler/`](../examples/project-euler/) | PE #1–#25 verified + runner (#26–#50 stubs stay in repo, not in playground) |
| **Benchmarks** | [`benchmarks/`](../examples/benchmarks/) | Links to `benchmarks/count.json` |
| **QA** | [`qa/`](../examples/qa/) | `bug-farm`, `invalid` |

| Folder | Programs | Topic |
|--------|----------|-------|
| [`00-getting-started/`](../examples/curriculum/00-getting-started/) | `hello`, `exitcode` | Hello world, exit codes |
| [`01-arithmetic/`](../examples/curriculum/01-arithmetic/) | `simple`, `move-values`, … | `mov`, `add`, inc/dec |
| [`02-types/`](../examples/curriculum/02-types/) | signed/unsigned compare | Typed comparisons |
| [`03-control-flow/`](../examples/curriculum/03-control-flow/) | `count`, `if-else` | Loops, branches |
| [`04-procedures/`](../examples/curriculum/04-procedures/) | `add-two`, factorial, … | Procedures & ABI |
| [`05-memory/`](../examples/curriculum/05-memory/) | pointer, stack-array, `int64[N]`, `byte[N]`, string-length, … | Level 3 memory curriculum |
| [`06-abi/`](../examples/curriculum/06-abi/) | stack-alignment, callee-saved, … | ABI edge cases |
| [`07-interop/`](../examples/interop/07-interop/) | `export-lib` | Shared library export |
| [`08-ai-agent/`](../examples/curriculum/08-ai-agent/) | `smoke-test` | Agent workflow |
| [`10-real-tools/`](../examples/tools/10-windows/) | `listfiles`, `filesize`, `exists`, … `machine`, `netcheck`, `tcpget`, `httpget`, `curl`, `dnslookup`, `cpucount`, `diskfree`, `procmem`, `loadavg`, `machine2` | Daily-use tools; systems introspection + TCP/HTTP (local fixture tests) |
| [`11-csharp-interop-real/`](../examples/interop/11-csharp-interop-real/) | `native_count_lines`, `native_fnv1a`, `native_sum_bytes` | HlaX64 DLL + C# P/Invoke caller with expected output |
| [`98-bug-farm/`](../examples/qa/bug-farm/) | (planned) | Compiler stress cases |
| [`99-invalid/`](../examples/qa/invalid/) | (planned) | Negative examples with expected diagnostics |

Tutorial for memory: [tutorials/05-memory.md](tutorials/05-memory.md).

Each folder has a README with build/run commands.

### Playground catalog

Every `.hla64` under `examples/` is listed in [playground/manifest.json](playground/manifest.json), grouped like this tree:

| Playground category | Groups | Count |
|---------------------|--------|------:|
| Curriculum | `00-getting-started` … `08-ai-agent` | 46 |
| Interop | `07-interop`, `11-csharp-interop-real` | 6 |
| Tools | `10-windows`, `12-linux` | 66 |
| Project Euler | `problems`, `runner` | 26 |
| QA | `bug-farm`, `invalid` | 37 |

Open [playground/index.html](playground/index.html) or regenerate cache:

```bash
./scripts/generate-playground-manifest.ps1
./scripts/generate-playground-cache.ps1
```

---

## 2. Integration samples (`tests/samples/`)

20 native samples with `manifest.json`:

```bash
hla64 test tests/samples/
```

Includes Level 3: `pointer_load_store`, `stack_array`, `string_length`, `array_sum`, `array_max`.

---

## 2b. Curriculum manifests (`tests/examples-curriculum/`)

96 manifests reference structured programs under `examples/`:

```bash
hla64 test tests/examples-curriculum/
hla64 test tests/examples-curriculum --filter real- --compile-only
```

See [tutorials/01-getting-started.md](tutorials/01-getting-started.md).

---

## 3. Commands

```bash
dotnet build

hla64 build examples/curriculum/05-memory/string-length.hla64 -o build/strlen
hla64 explain examples/curriculum/05-memory/stack-array.hla64 --json

hla64 test tests/samples
hla64 test tests/examples-curriculum
hla64 test tests/samples --json
```

Machine-readable schemas: [`schemas/`](../schemas/).

---

## 4. Manifest format

```json
{
  "name": "hello",
  "source": "hello.hla64",
  "expectedStdout": "Hello from HlaX64\n",
  "expectedExitCode": 0
}
```

---

## 5. See also

- [`memory-and-bounds.md`](./memory-and-bounds.md) — pointer model & UB
- [`runtime-matrix.md`](./runtime-matrix.md) — target × output defaults
- [`classic-hla-comparison.md`](./classic-hla-comparison.md) — HLA vs HlaX64
- [`development.md`](./development.md) — contributor setup
