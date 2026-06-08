# AI Agent Workflow

Use MCP tools or CLI JSON to verify generated routines:

```bash
# Explain lowering (human)
hla64 explain examples/curriculum/08-ai-agent/smoke-test.hla64

# JSON for agents
hla64 explain examples/curriculum/08-ai-agent/smoke-test.hla64 --json
hla64 test tests/samples --json
```

## MCP tools (stdio server)

| Tool | Purpose |
|------|---------|
| `explain` | IR + ABI + NASM JSON |
| `format-source` | Normalize `.hla64` layout |
| `doctor` | Toolchain readiness |
| `compile` / `build` / `run` / `test` | Pipeline execution |

See [docs/mcp-tools.md](../../docs/mcp-tools.md) and [docs/mcp-security.md](../../docs/mcp-security.md).
