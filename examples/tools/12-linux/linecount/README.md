# linecount (Linux)

Counts `\n` bytes in `argv[1]` using `open` / `read` / `close` from `libc.so` and the Linux SysV argv runtime.

Reuses the Windows `linecount` fixture `sample-b.txt` (2 lines).

```bash
hla64 build examples/tools/12-linux/linecount/linecount.hla64 --target linux-x64-sysv -o build/linux-linecount
./build/linux-linecount/linecount examples/tools/10-windows/linecount/fixtures/sample-b.txt
```

Native WSL regression: `LinuxRealTool_linecount_runs_under_wsl_when_available`.
