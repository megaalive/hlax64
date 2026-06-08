# cmp

Byte-compares two files (`argv[1]`, `argv[2]`) up to the first 256 bytes read from each.

## Usage

```
cmp.exe <file-a> <file-b>
```

Prints `identical` (exit 0), `differ at N`, or `differ: size`.

## Expected

Comparing `sample-a.txt` to itself: `identical`, exit `0`.
