# Tutorial 2 — Native Routines

Learn procedures, control flow, comparisons, and ABI basics using the structured `examples/` curriculum.

## Procedures (SysV AMD64)

Linux passes the first six integer arguments in `rdi`, `rsi`, `rdx`, `rcx`, `r8`, `r9`. Return value is in `rax`.

```bash
hla64 run examples/curriculum/04-procedures/add-two.hla64
# stdout: 10 + 20 = 30
```

Study:

- `no-args.hla64` — procedure with no parameters
- `six-args.hla64` — all six register arguments
- `factorial-iter.hla64` — loop in a procedure; exit code 120 (5!)

## Control flow

```bash
hla64 run examples/curriculum/03-control-flow/count.hla64
hla64 run examples/curriculum/03-control-flow/if-else.hla64
```

`count.hla64` demonstrates `while` / `endwhile`. `if-else.hla64` uses `if` / `else` / `endif`.

## Signed vs unsigned compares

```bash
hla64 run examples/curriculum/02-types/signed-compare.hla64
hla64 run examples/curriculum/02-types/unsigned-compare.hla64
```

Signed uses `<`, `>`, `=`. Unsigned uses `>?`, `<?` (HlaX64 extensions).

## Stack and callee-saved registers

```bash
hla64 run examples/curriculum/06-abi/stack-alignment.hla64
hla64 run examples/curriculum/06-abi/callee-saved.hla64
```

These verify the compiler preserves the SysV ABI when calling nested procedures.

## Memory on the stack

```bash
hla64 run examples/curriculum/05-memory/sum-1-to-5.hla64
# stdout: sum 1..5 = 15
```

## Explain before you run

For any example:

```bash
hla64 explain examples/curriculum/04-procedures/six-args.hla64 --json
hla64 explain-abi --target linux-x64-sysv
```

Compare generated register usage with the ABI table from `explain-abi`.

## Windows target (optional)

Build for MS ABI (requires Windows linker):

```bash
hla64 build examples/curriculum/06-abi/windows-exitcode.hla64 \
  --target windows-x64-msabi -o build/win-exit
```

## Test manifests

Curriculum programs have native test manifests:

```bash
hla64 test tests/examples-curriculum
```

## Next steps

- [Tutorial 3 — C# interop](03-csharp-interop.md)
- [docs/classic-hla-comparison.md](../classic-hla-comparison.md)
- RFC [0002-pointer-model](../../rfcs/0002-pointer-model.md)
