# Tutorial 1 — Getting Started

Build and run your first HlaX64 programs in about 15 minutes.

## 1. Install

Follow [install.md](../install.md). Quick check:

```bash
hla64 doctor
hla64 --version
```

## 2. Hello world

Open `examples/00-getting-started/hello.hla64`:

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

Run:

```bash
hla64 run examples/00-getting-started/hello.hla64
```

Expected stdout: `Hello from HlaX64`

## 3. Inspect the pipeline

See IR and NASM without running:

```bash
hla64 explain examples/00-getting-started/hello.hla64
hla64 emit-nasm examples/00-getting-started/hello.hla64 -o build/hello.nasm
```

Use `--json` for agent-friendly output.

## 4. Exit codes

Programs return the value in `rax` when they finish:

```bash
hla64 run examples/00-getting-started/exitcode.hla64
echo $?    # Linux: 42
```

Source: `examples/00-getting-started/exitcode.hla64`.

## 5. Arithmetic

Work through `examples/01-arithmetic/`:

| File | Lesson |
|------|--------|
| `simple.hla64` | `mov`, `add` |
| `subtract.hla64` | `sub` |
| `move-values.hla64` | Register-to-register moves |
| `inc-dec.hla64` | `inc`, `dec` |

Build one explicitly:

```bash
hla64 build examples/01-arithmetic/simple.hla64 -o build/simple
./build/simple
echo $?   # 3
```

## 6. Run curriculum tests

Manifests under `tests/examples-curriculum/` point at these examples:

```bash
hla64 test tests/examples-curriculum --filter hello
hla64 test tests/examples-curriculum
```

## Next steps

- [Tutorial 2 — Native routines](02-native-routines.md)
- [examples/README.md](../../examples/README.md)
- [language-spec.md](../language-spec.md)
