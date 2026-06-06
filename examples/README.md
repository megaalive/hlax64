# HlaX64 Examples

Each example is a standalone `.hla64` program. Run with:

```bash
hla64 run examples/<name>.hla64
# or
dotnet run --project src/HlaX64.Cli -- run examples/<name>.hla64
```

## Getting Started

| Example | Description |
|---------|-------------|
| [`hello.hla64`](hello.hla64) | Print "Hello from HlaX64" to stdout |
| [`exitcode.hla64`](exitcode.hla64) | Return a specific exit code (42) |

## Arithmetic

| Example | Description |
|---------|-------------|
| [`add_two.hla64`](add_two.hla64) | Procedure with two arguments using `add` |
| [`simple.hla64`](simple.hla64) | Basic arithmetic operations |

## Control Flow

| Example | Description |
|---------|-------------|
| [`count.hla64`](count.hla64) | `while` loop counting 0 to 4 with register print |

## Full List

| File | Topic | Features Demonstrated |
|------|-------|----------------------|
| `hello.hla64` | Hello world | `stdout.put`, string literal, newline |
| `exitcode.hla64` | Exit code | `mov`, program exit via `rax` |
| `add_two.hla64` | Procedure | `procedure`, `@returns`, `call`, `add` |
| `simple.hla64` | Arithmetic | Register moves, basic instructions |
| `count.hla64` | Loop | `while`/`endwhile`, comparison, `stdout.put` with register |
