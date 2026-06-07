# native_count_lines

Exports `CountLines(data, length)` — the same newline-count loop as `10-real-tools/linecount`, but as a shared library for C#.

## What it stresses

- `export procedure` + shared-library link (no `_start` / `ExitProcess` in the DLL)
- Pointer parameter (`ptr`) across the native/managed boundary
- Win64 MS ABI — use **volatile** registers (`r8`–`r11`, `r10`) inside exports; callee-saved regs break the .NET caller

## Expected

With `fixtures/sample-b.txt` (`bravo\ncharlie\n`): stdout `lines: 2`, exit `0`.
