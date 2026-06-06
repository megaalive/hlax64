# ABI Examples

| Example | Topic |
|---------|-------|
| [stack-alignment.hla64](stack-alignment.hla64) | Call preserves stack alignment |
| [callee-saved.hla64](callee-saved.hla64) | Procedure call + return value |

Use `hla64 explain-abi --target linux-x64-sysv` for register assignment tables.

Windows MS ABI: add `--target windows-x64-msabi` to build commands.
