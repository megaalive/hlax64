# wc

Counts lines, words, and bytes in the file passed as `argv[1]`.

File access uses `hlax_file_open_read`, `hlax_file_read`, and `hlax_file_close` from the runtime stdlib. Whitespace detection delegates to `hlax_is_space`.

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
