# cat (Linux)

Copy `argv[1]` bytes to stdout using `hlax_file_*` read loop and `hlax_stdout_write`.

```bash
hla64 build examples/tools/12-linux/cat/cat.hla64 --target linux-x64-sysv -o build/linux-cat
./build/linux-cat examples/tools/12-linux/cat/fixtures/sample-cat.txt
```
