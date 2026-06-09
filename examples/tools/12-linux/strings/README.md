# strings (Linux)

Print ASCII printable runs of length >= 4 from `argv[1]` using `hlax_is_printable` and `hlax_stdout_write`.

```bash
hla64 build examples/tools/12-linux/strings/strings.hla64 --target linux-x64-sysv -o build/linux-strings
./build/linux-strings examples/tools/12-linux/strings/fixtures/sample-strings.txt
```
