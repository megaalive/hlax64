# cp (Linux)

Copy `argv[1]` bytes to `argv[2]` using `hlax_file_open_write` and a read/write loop.

```bash
hla64 build examples/tools/12-linux/cp/cp.hla64 --target linux-x64-sysv -o build/linux-cp
./build/linux-cp src.txt dst.txt
```

Native WSL regression uses `$OUTPUT` in `expected.arguments` and checks `expected.output`.
