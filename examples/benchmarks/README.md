# Benchmarks

Run timed samples from the repo manifest:

```bash
hla64 bench benchmarks/count.json
hla64 bench benchmarks/count.json --json
```

JSON mode prints machine-readable output that matches
[`schemas/benchmark-result.schema.json`](../../schemas/benchmark-result.schema.json):

```json
{
  "schemaVersion": 1,
  "version": "0.1.0",
  "results": [
    {
      "name": "count",
      "meanMs": 1.26,
      "medianMs": 1.18,
      "minMs": 1.03,
      "maxMs": 1.54,
      "stdDevMs": 0.21,
      "iterations": 10,
      "warmupIterations": 3,
      "compileDurationMs": 84.1,
      "binarySizeBytes": 24576
    }
  ]
}
```

Manifest entries reference curriculum examples under `examples/`.
