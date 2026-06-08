# exists

Checks whether the path at `argv[1]` exists and returns exit code `0` when it does, `1` when missing.

This is the smallest Win32 interop smoke test in the real-tools folder.

## Usage

```
exists.exe <path>
```

## Expected

With `fixtures/sample-a.txt`: stdout `exists`, exit `0`.
