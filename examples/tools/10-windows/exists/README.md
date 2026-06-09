# exists

Checks whether the path at `argv[1]` exists and returns exit code `0` when it does, `1` when missing.

Uses the cross-platform runtime helper `hlax_path_exists` from `stdlib64.hhf` (no raw Win32/Linux `extern` boilerplate).

## Usage

```
exists.exe <path>
```

## Expected

With `fixtures/sample-a.txt`: stdout `exists`, exit `0`.
