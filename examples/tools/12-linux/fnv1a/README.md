# fnv1a (Linux)

FNV-1a 64-bit hash of `argv[1]` using `stdout.putu` for unsigned decimal output.

Expected hash for `sample-a.txt` (`alpha\n`): **13533948706745587731**.

```bash
hla64 build examples/tools/12-linux/fnv1a/fnv1a.hla64 --target linux-x64-sysv -o build/linux-fnv1a
```
