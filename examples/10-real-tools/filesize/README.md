# filesize

Prints the byte size of the file at `argv[1]` using `FindFirstFileA` metadata.

This keeps the `listfiles` size path as a smaller standalone example.

## Usage

```
filesize.exe <file>
```

## Expected

With `fixtures/sample-a.txt`: stdout `bytes: 6`, exit `0`.
