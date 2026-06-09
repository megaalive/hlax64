# tee (Linux)

Copy stdin bytes to stdout and `argv[1]` using `hlax_file_read(stdin_fd, ...)` plus `hlax_stdout_write` and `hlax_file_write`.

```bash
hla64 build examples/tools/12-linux/tee/tee.hla64 --target linux-x64-sysv -o build/linux-tee
printf 'hello\n' | ./build/linux-tee out.txt
```

Native WSL regression pipes `expected.stdin` and verifies stdout plus `expected.output`.
