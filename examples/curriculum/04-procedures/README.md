# Procedures

| Example | Description |
|---------|-------------|
| [add-two.hla64](add-two.hla64) | Two-argument procedure, `@returns("rax")`, `call` |
| [factorial-iter.hla64](factorial-iter.hla64) | Iterative factorial loop (5! = 120 exit) |
| [factorial-rec.hla64](factorial-rec.hla64) | Recursive factorial (5! = 120 exit) |

ABI: first two integer args in `rdi`, `rsi`; return in `rax`.
