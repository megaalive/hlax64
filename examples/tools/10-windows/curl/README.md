# curl

Networking vertical slice: HTTP/1.0 GET with DNS (`hlax_dns_resolve_v4` + `hlax_tcp_connect`), body-only output by default (`-i` includes response headers). Requires a local TCP fixture for integration tests (see `expected.server`).
