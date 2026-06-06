# HlaX64 — Examples & How to Run

> **Direktori**: [`examples/`](../examples) (structured curriculum) ·
> [`tests/samples/`](../tests/samples) (integration tests with manifests).

Katalog program contoh HlaX64. Lihat [`language-spec.md`](./language-spec.md) untuk referensi bahasa.

---

## 1. Structured examples (`examples/`)

| Folder | Programs | Topic |
|--------|----------|-------|
| [`00-getting-started/`](../examples/00-getting-started/) | `hello`, `exitcode` | Hello world, exit codes |
| [`01-arithmetic/`](../examples/01-arithmetic/) | `simple` | `mov`, `add` |
| [`03-control-flow/`](../examples/03-control-flow/) | `count`, `if-else` | Loops, branches |
| [`04-procedures/`](../examples/04-procedures/) | `add-two` | Procedures & SysV ABI |

Each folder has a README with build/run commands.

---

## 2. Integration samples (`tests/samples/`)

17 samples with `manifest.json`, run via `hla64 test tests/samples`.

## 2b. Curriculum manifests (`tests/examples-curriculum/`)

19 manifests reference structured programs under `examples/`:

```bash
hla64 test tests/examples-curriculum
```

See [tutorials/01-getting-started.md](tutorials/01-getting-started.md).

| Sample | Topic |
|--------|-------|
| `hello/`, `exitcode/`, `add_two/`, `count/`, `simple/` | Core language |
| `local_var/`, `if_else/` | Variables, control flow |
| `procedure_*` | 0, 1, 6 arguments |
| `comparison_*` | Signed / unsigned compares |
| `comparison_uint64_boundary/` | High-bit uint64 (`0x8000…0000`) vs 1 |
| `stdout_int64/`, `callee_saved/`, `stack_alignment/` | ABI edge cases |
| `export_lib/` | Shared library + interop |

---

## 3. Commands

```bash
dotnet build

# Emit NASM
dotnet run --project src/HlaX64.Cli -- emit-nasm examples/00-getting-started/hello.hla64

# Build executable
dotnet run --project src/HlaX64.Cli -- build examples/00-getting-started/exitcode.hla64 -o build/exitcode

# Run
dotnet run --project src/HlaX64.Cli -- run examples/00-getting-started/hello.hla64

# Test suite
dotnet run --project src/HlaX64.Cli -- test tests/samples
dotnet run --project src/HlaX64.Cli -- test tests/samples --filter hello
dotnet run --project src/HlaX64.Cli -- test tests/samples --json
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

- [`runtime-matrix.md`](./runtime-matrix.md) — target × output defaults
- [`classic-hla-comparison.md`](./classic-hla-comparison.md) — HLA vs HlaX64
- [`development.md`](./development.md) — contributor setup
