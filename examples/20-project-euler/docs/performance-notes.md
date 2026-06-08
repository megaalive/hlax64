# Performance notes (early Euler)

Work in progress. Compare bruteforce vs formula for #1 using:

```bash
hla64 bench examples/20-project-euler/problems/euler001-multiples-of-3-and-5-bruteforce.hla64
hla64 bench examples/20-project-euler/problems/euler001-multiples-of-3-and-5-formula.hla64
```

Inspect generated NASM (`hla64 explain …`) for loop size and `idiv`/`mod` frequency.

| Problem | Naive | Optimized | Notes |
|---------|-------|-----------|-------|
| #1 | O(n) loop + 2× mod | O(1) arithmetic | Formula removes hot loop |
| #2 | O(log φ) Fib steps | same | Single pass, no recursion |
| #3 | trial division | skip evens after 2 | See #3 source |

Future: `-O1`/`-O2` impact on Euler loops once optimizer covers `mod` chains.
