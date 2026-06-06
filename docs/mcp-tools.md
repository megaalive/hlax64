# HlaX64 MCP Tools

The MCP server (`src/HlaX64.McpServer/`) exposes tools over stdio JSON-RPC. Tool names use kebab-case (e.g. `explain-abi`).

## Tools

| Tool | Description |
|------|-------------|
| `compile` | Source → NASM text |
| `build` | Source → native executable or shared library |
| `run` | Build + execute; returns JSON `{ exit_code, stdout, stderr }` |
| `test` | Run manifest directory; returns pass/fail JSON |
| `explain-abi` | ABI register assignment for a target triple |
| `explain` | IR + lowered ABI + NASM JSON (`schemaVersion`) |
| `format-source` | Format file in place; optional `check: true` |
| `doctor` | Toolchain readiness JSON |
| `generate-header` | C header for exported procedures |
| `generate-pinvoke` | C# `[DllImport]` wrappers |
| `get-version` | Compiler version string |
| `list-instructions` | Supported mnemonic list |

## Example agent flow

1. `doctor` — verify NASM/linker
2. `explain` — audit generated lowering before run
3. `test` — run `tests/samples` manifests
4. `build` / `run` — produce and execute binary

## Security

Restrict workspace paths and treat `run`/`test` as executing untrusted native code. See [mcp-security.md](mcp-security.md).

## Running the server

```bash
dotnet run --project src/HlaX64.McpServer
```

Configure your MCP client to launch the command above with working directory set to the repository root.
