# listfiles

Lists files matching the glob at `argv[1]` and prints each name plus byte size.

Typical usage passes a directory glob such as `fixtures\\*`.

This is the regression example that catches Win32 struct layout, `.dword` memory loads, `shl(32, reg)`, nested `if/while`, and multi-argument `stdout.put` issues.

## Usage

```
listfiles.exe <dir\\*>
```

## Expected

With `fixtures\\*`: lists `sample-a.txt` and `sample-b.txt` with sizes, exit `0`.
