# Tutorial 4 — MCP Agent Integration

Use the HlaX64 MCP server so an AI agent can compile, explain, test, and run native code safely.

## 1. Start the server

From the repository root:

```bash
dotnet run --project src/HlaX64.McpServer
```

The server speaks JSON-RPC over **stdio**. Configure your MCP client (Cursor, Claude Desktop, etc.) to launch this command with `cwd` set to the repo (or an allowed workspace).

## 2. Recommended agent flow

```text
doctor → explain → test → build/run
```

| Step | Tool | Why |
|------|------|-----|
| 1 | `doctor` | Confirm NASM + linker before native work |
| 2 | `explain` | Audit IR/NASM without executing |
| 3 | `test` | Run manifest suites (`tests/samples` or curriculum) |
| 4 | `build` / `run` | Produce binary when outputs look correct |

## 3. Example: smoke test

Curriculum program `examples/08-ai-agent/smoke-test.hla64`:

```bash
# CLI equivalent
hla64 run examples/08-ai-agent/smoke-test.hla64
```

Via MCP, call `run` with `sourcePath` pointing at that file. Expect stdout containing `verified routine ok`.

Use `explain` first to show the agent (and human reviewer) the lowered NASM.

## 4. Format and diagnostics

| Tool | Purpose |
|------|---------|
| `format-source` | Normalize `.hla64` formatting (`check: true` for CI) |
| `explain` | Structured JSON with `schemaVersion` |
| `list-instructions` | Valid mnemonics for codegen prompts |

Schemas live in [`schemas/`](../../schemas/).

## 5. Security

Treat `run` and `test` as ** executing native binaries** in the agent's environment.

- Restrict filesystem paths to trusted workspaces
- Read [mcp-security.md](../mcp-security.md)
- Prefer `explain` + `test` on known manifests before arbitrary `run`

## 6. Cursor configuration sketch

Add to MCP settings (client-specific):

```json
{
  "mcpServers": {
    "hla64": {
      "command": "dotnet",
      "args": ["run", "--project", "src/HlaX64.McpServer"],
      "cwd": "/path/to/hlax64"
    }
  }
}
```

Use an absolute path to your clone. After install from release, point `command` at `hla64` with args invoking MCP if packaged separately.

## 7. Static playground

For human exploration without MCP, open [playground/index.html](../playground/index.html) (also on GitHub Pages when enabled).

Design notes: [playground-design.md](../playground-design.md).

## Tool reference

Full list: [mcp-tools.md](../mcp-tools.md).

## Next steps

- [architecture.md](../architecture.md)
- [CONTRIBUTING.md](../rules/CONTRIBUTING.md) — add MCP tools or manifests
