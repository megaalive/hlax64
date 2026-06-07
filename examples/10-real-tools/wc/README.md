# wc

Counts lines, words, and bytes in the file passed as `argv[1]`.

## Stress

Argv runtime, byte classification, multiple counters, `IsSpace` helper, calls inside loop with callee-saved line/word state.

## Usage

```
wc.exe <file>
```

## Expected

```
lines=2 words=2 bytes=14
```

(with `fixtures/sample-b.txt`). Exit code `0`.
