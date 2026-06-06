# Security Policy

## Supported Versions

| Version | Supported          |
|---------|--------------------|
| 0.x     | ✅ (development)   |

## Reporting a Vulnerability

HlaX64 compiles source code into native executables. A vulnerability could allow arbitrary code execution during compilation or through the MCP server.

To report a security vulnerability:

1. **Do not** file a public GitHub issue
2. Email: `megaalive@users.noreply.github.com`
3. Include:
   - Description of the vulnerability
   - Steps to reproduce
   - Affected version(s)
   - Potential impact

You will receive an acknowledgment within 48 hours. We will work on a fix and coordinate disclosure.

## Security-Critical Areas

- **Compiler code generation** — must not emit incorrect or exploitable assembly
- **Linker invocation** — untrusted object file paths could lead to command injection
- **MCP server** — executes arbitrary compile/test/run commands; workspace and path access must be restricted
- **Native binary execution** — `hla64 run` executes compiled output; treat as untrusted
- **Benchmark runner** — executes arbitrary commands from JSON manifests
- **Dependencies** — keep NuGet packages updated; review transitive dependencies

## MCP Server Security

The MCP server (`src/HlaX64.McpServer/`) accepts JSON-RPC requests over stdin/stdout. When exposing it to AI agents:

- Restrict the working directory to the project workspace
- Validate all file paths to prevent directory traversal
- Apply timeouts to compile/run operations
- Do not expose the MCP server to untrusted networks
- Review which tools are available (see `tools/list`)

## Build Toolchain

Users are expected to provide their own NASM, GCC/ld, or WSL installation. The CLI auto-detects these tools. Ensure toolchain binaries come from trusted sources.
