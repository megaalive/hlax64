# linecount

Counts newline bytes in a file passed as `argv[1]`.

This example stresses `CreateFileA`, `ReadFile`, the Windows argv runtime (`hlax_argv_*`), byte loads, pointer increments, and loop state over data read from the OS.

Native regression: `expected.arguments` points at `fixtures/sample-b.txt`; stdout is `lines: 2`.

Getting the first Win32 file-I/O version to execute uncovered and fixed two real compiler bugs: a Win64 stack-argument alignment bug (odd stack-arg counts such as `CreateFileA`'s 7 arguments produced a non-16-byte-aligned `sub rsp`) and a control-flow lowering bug where a `while` loop containing an `if` laid the loop continuation block after the `if` body, causing an infinite loop.

## Usage

```
linecount.exe <file>
```

Without a file argument the program prints `usage: linecount <file>` and exits with code 1.
