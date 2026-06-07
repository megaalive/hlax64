# MCP Server Security

The HlaX64 MCP server (`src/HlaX64.McpServer/`) exposes compiler, build, run, test, and interop tools to AI agents over **stdio JSON-RPC**.

See also [SECURITY.md](rules/SECURITY.md) for vulnerability reporting.

## Threat model

| Risk | Description |
|------|-------------|
| **Arbitrary code execution** | `run` and `test` execute compiled native binaries |
| **Command injection** | Toolchain invocation must not pass untrusted shell fragments |
| **Path traversal** | File paths from agents must stay within the workspace |
| **Resource exhaustion** | Compile/run loops without timeouts can hang or exhaust disk |
| **Supply chain** | External NASM/linker binaries must come from trusted sources |

## Recommended deployment

### Workspace restriction

- Run the MCP server with **current working directory** set to the project root.
- Do not expose the server to **untrusted networks**; stdio is intended for local IDE/agent integration only.

### Path validation

- Reject paths containing `..` that escape the workspace root.
- Prefer relative paths under the repository.
- Do not follow symlinks outside the workspace (host-dependent).

### Execution limits

| Control | Recommendation |
|---------|----------------|
| **Timeout** | Cap compile and run duration (default test timeout: 10s per sample) |
| **Output size** | Limit captured stdout/stderr in agent responses |
| **Concurrent runs** | Serialize or cap parallel `run`/`test` invocations |
| **Disk** | Use ephemeral `build/` directories; clean up after sessions |

### Environment

- Filter sensitive environment variables before spawning toolchain processes.
- Do not pass API keys or credentials into child processes unless required.
- **Network access** is not required for core compile/test; disable outbound network in sandboxed CI if possible.

### Tool surface

Review `tools/list` before connecting an agent. Disable or wrap tools you do not need:

| Tool | Risk level |
|------|------------|
| `compile` / `emit-nasm` | Low — generates text |
| `build` | Medium — invokes NASM + linker |
| `run` / `test` | **High** — executes native code |
| `bench` | **High** — repeated execution |
| `generate-header` / `generate-pinvoke` | Low — text generation |
| `explain-abi` / `get-version` | Low |

### Unsafe mode

There is **no official "unsafe mode"** that disables sandboxing. If you add one for local debugging, it must:

- Be opt-in via explicit flag or environment variable
- Log loudly when enabled
- Never be default in published MCP configurations

## Toolchain trust

Users provide their own NASM, GCC, Clang, LLD, and WSL installations. Verify binaries from official packages or package managers.

## Agent prompt guidance

When configuring AI agents:

1. Prefer `test` with known manifests over ad-hoc `run` of generated code.
2. Use `doctor` before first build on a new machine.
3. Treat all generated executables as **untrusted** until reviewed.
4. Do not ask the agent to compile or run code from untrusted third-party sources without review.

## Reporting issues

Security issues in MCP path handling or toolchain invocation: see [SECURITY.md](rules/SECURITY.md). Do not file public issues for exploitable vulnerabilities.
