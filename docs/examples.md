# HlaX64 — Examples & How to Run

> **Direktori**: [`examples/`](../examples) (structured curriculum) ·
> [`tests/samples/`](../tests/samples) (integration tests with manifests).

---

## 1. Structured examples (`examples/`)

| Folder | Programs | Topic |
|--------|----------|-------|
| [`00-getting-started/`](../examples/00-getting-started/) | `hello`, `exitcode` | Hello world, exit codes |
| [`01-arithmetic/`](../examples/01-arithmetic/) | `simple`, `move-values`, … | `mov`, `add`, inc/dec |
| [`02-types/`](../examples/02-types/) | signed/unsigned compare | Typed comparisons |
| [`03-control-flow/`](../examples/03-control-flow/) | `count`, `if-else` | Loops, branches |
| [`04-procedures/`](../examples/04-procedures/) | `add-two`, factorial, … | Procedures & ABI |
| [`05-memory/`](../examples/05-memory/) | pointer, stack-array, `int64[N]`, `byte[N]`, string-length, … | Level 3 memory curriculum |
| [`06-abi/`](../examples/06-abi/) | stack-alignment, callee-saved, … | ABI edge cases |
| [`07-interop/`](../examples/07-interop/) | `export-lib` | Shared library export |
| [`08-ai-agent/`](../examples/08-ai-agent/) | `smoke-test` | Agent workflow |
| [`10-real-tools/`](../examples/10-real-tools/) | `listfiles`, `filesize`, `exists`, `linecount`, `hexdump`, `wc`, `fnv1a`, `filemagic`, `cmp` | Daily-use Win32 tools; each tool has `fixtures/`, `expected.stdout`, native Windows regression |
| [`11-csharp-interop-real/`](../examples/11-csharp-interop-real/) | `native_count_lines` | HlaX64 DLL + C# P/Invoke caller with expected output |
| [`98-bug-farm/`](../examples/98-bug-farm/) | (planned) | Compiler stress cases |
| [`99-invalid/`](../examples/99-invalid/) | (planned) | Negative examples with expected diagnostics |

Tutorial for memory: [tutorials/05-memory.md](tutorials/05-memory.md).

Each folder has a README with build/run commands.

---

## 2. Integration samples (`tests/samples/`)

20 native samples with `manifest.json`:

```bash
hla64 test tests/samples/
```

Includes Level 3: `pointer_load_store`, `stack_array`, `string_length`, `array_sum`, `array_max`.

---

## 2b. Curriculum manifests (`tests/examples-curriculum/`)

39 manifests reference structured programs under `examples/`:

```bash
hla64 test tests/examples-curriculum/
hla64 test tests/examples-curriculum --filter real- --compile-only
```

See [tutorials/01-getting-started.md](tutorials/01-getting-started.md).

---

## 3. Commands

```bash
dotnet build

hla64 build examples/05-memory/string-length.hla64 -o build/strlen
hla64 explain examples/05-memory/stack-array.hla64 --json

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
