# native_sum_bytes

Exports `SumBytes(data, length)` — adds every byte in the buffer. Smallest useful interop sanity check before heavier loops (FNV, line count).

## Expected

With `fixtures/sample-a.txt` (`alpha\n`, six bytes): stdout `sum=528`, exit `0`.

C# baseline: `bytes.Sum(b => (long)b)`.
