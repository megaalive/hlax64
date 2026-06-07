# exists (Linux)

Returns exit code 0 when `argv[1]` exists (`access(path, F_OK)`), else 1.

```bash
hla64 build examples/12-real-tools-linux/exists/exists.hla64 --target linux-x64-sysv -o build/linux-exists
```
