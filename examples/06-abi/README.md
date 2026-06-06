# ABI Examples

| Example | Topic |
|---------|-------|
| [stack-alignment.hla64](stack-alignment.hla64) | Call preserves stack alignment |
| [callee-saved.hla64](callee-saved.hla64) | Procedure call + return value |
| [stack-args-sysv.hla64](stack-args-sysv.hla64) | Eight integer args (6 registers + 2 stack, SysV) |

Use `hla64 explain-abi --target linux-x64-sysv` for register assignment tables.

Windows MS ABI build:

```bash
hla64 build examples/06-abi/windows-exitcode.hla64 --target windows-x64-msabi -o build/win-exit
```
