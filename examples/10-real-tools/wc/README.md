# wc

Counts lines, words, and bytes in `fixtures/sample-b.txt` (hardcoded path until argv).

## Stress

Byte classification, multiple counters, `IsSpace` helper, calls inside loop with callee-saved line/word state.

## Expected

```
lines=2 words=2 bytes=14
```

Exit code `0`.
