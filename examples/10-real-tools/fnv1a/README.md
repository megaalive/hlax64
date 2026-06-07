# fnv1a

Computes FNV-1a 64-bit over the file passed as `argv[1]`.

## Stress

Argv runtime, `xor` / `imul` hot loop, hex `$` constants, file read.

## Expected stdout

```
fnv1a=4912795366963963885
```

This is the **signed** decimal rendering of the correct unsigned hash (`13533948706745587731`). `stdout.put` currently prints int64 as signed.

## Baseline

Verify with Python:

```python
h = 0xcbf29ce484222325
for b in open("fixtures/sample-a.txt","rb").read():
    h = ((h ^ b) * 0x100000001b3) & ((1<<64)-1)
print(h)
```
