# Tutorial 07 — Systems & Networking

This tutorial covers the **non-MVP systems and networking runtime** shipped in
`src/HlaX64.Runtime/` and the matching tools under `examples/tools/`.

## Runtime modules

| Module | Symbols | Purpose |
|--------|---------|---------|
| `sys.nasm` | `hlax_getpid`, `hlax_hostname`, `hlax_mem_*`, `hlax_file_size`, `hlax_os_last_error`, `hlax_cpu_count`, `hlax_disk_*`, `hlax_self_rss_bytes`, `hlax_load_avg_milli` | Machine and process introspection |
| `net.nasm` | `hlax_net_*`, `hlax_tcp_*`, `hlax_dns_resolve_v4` | IPv4 TCP client, DNS, timeouts |

See [`runtime-contract.md`](../runtime-contract.md) for the full ABI table and
tool exit-code convention (`0` ok, `1` usage, `2` OS, `3` network).

## Quick tools (playground-friendly)

These run without a local TCP server:

```bash
hla64 run examples/tools/10-windows/pid/pid.hla64
hla64 run examples/tools/10-windows/cpucount/cpucount.hla64
hla64 run examples/tools/10-windows/diskfree/diskfree.hla64 -- .
hla64 run examples/tools/10-windows/procmem/procmem.hla64
hla64 run examples/tools/10-windows/dnslookup/dnslookup.hla64 -- localhost
```

`loadavg` prints `unsupported` on Windows; on Linux it reports `load_milli=`.

## Network tools (local fixture required)

`netcheck`, `tcpget`, and `httpget` connect to `host:port` from argv. Integration
tests start a **local TCP fixture** on `$PORT` (see `expected.server` in each tool
directory). They are not playground-safe without that server.

Example harness tokens in `expected.arguments`:

- `$HOST` — loopback on native Windows; WSL tests use the Windows host IP
- `$PORT` — ephemeral port from the fixture

## Error reporting

On failure, tools print `err=<code>` using `hlax_os_last_error()` or
`hlax_net_last_error()`:

```text
connect failed
err=10061
```

## Limits (honest non-goals)

- IPv4 only (no IPv6)
- No TLS/HTTPS
- No ICMP ping
- DNS tests use `localhost` only (offline-friendly)
- Windows load average unsupported

## Next steps

- Read [`stdlib64.hhf`](../../src/HlaX64.Runtime/include/stdlib64.hhf) prototypes
- Compare `machine` vs `machine2` for expanded reporting
- Run curriculum manifests: `hla64 test tests/examples-curriculum/`
