# hexdump

Prints `00000000` plus space-separated hex bytes for `fixtures/sample-a.txt`.

## Stress

CreateFileA/ReadFile, pointer end-sentinel loop, hex nibble formatting via branch tree, nested calls inside loop (callee-saved cursor in `r14`, end in `r13`).

## Run

From repo root after building for `windows-x64-msabi`.

## Expected

```
00000000  61 6c 70 68 61 0a 
```

Exit code `0`.
