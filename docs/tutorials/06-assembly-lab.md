# Tutorial 06 — Assembly Lab

The **HlaX64 Assembly Lab** is a cross-platform desktop app for exploring the compile pipeline visually.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [NASM](https://nasm.us) and a linker (`gcc` on Linux, WSL or `lld-link` on Windows)
- Clone the repo and run `dotnet build`

## Launch

```bash
dotnet run --project src/HlaX64.AssemblyLab
```

On Windows, ensure NASM is on PATH (`scripts/setup-toolchain-path.ps1`).

## Workflow

### 1. Open a program

- **Open File** — pick any `.hla64` file (e.g. `examples/00-getting-started/hello.hla64`)
- **Open Folder** — pick a directory; if `hla64.toml` is present, the first `.hla64` source is loaded

### 2. Live diagnostics

Edit source in the **Source** tab (AvaloniaEdit with HlaX64 syntax highlighting and line numbers). Click the left gutter to toggle breakpoints (stored in the session; DAP wiring is follow-up). After a short debounce, diagnostics appear in the left panel with line numbers and HLAX codes.

### 3. Pipeline tabs

| Tab | Shows |
|-----|-------|
| **IR** | Lowered intermediate representation |
| **NASM** | Emitted assembly (matches `hla64 explain` / `emit-nasm`) |
| **Disasm** | NASM listing with source-map columns; objdump when a binary exists |
| **ABI** | Lowered functions, stack frames, verification hints |

Select **Target**: `linux-x64-sysv` (default) or `windows-x64-msabi`.

### 4. Build and run

- **Build** — compile, assemble, link; writes `build/<name>/` with `.nasm`, binary, and `.hlamap.json`
- **Run** — build then execute; exit code and stdout/stderr appear in **Output**

### 5. Source map sync

After **Build**, double-click a diagnostic or navigate by line. When a mapping exists, the **NASM** tab highlights the corresponding line (`>>>` prefix).

Try `examples/06-abi/stack-args-sysv.hla64` after building with source map enabled.

### 6. Plan approval gate

Review the **Plan** tab (compile/assemble/link steps). Check **Plan approved** before **Build**, **Run**, or **Proof Bundle**. The plan and **Diff** tab refresh when you edit source or change target; approval resets until you confirm again.

### 7. Agent explain / repair

Click **Explain** to run the in-process explain agent (same JSON shape as MCP `explain`, including `suggestedFix` per diagnostic). Results appear in the **Agent** tab.

### 8. Debug

**Debug** builds the program, spawns `hla64 debug --stdio`, and sends DAP initialize/launch/setBreakpoints (from gutter breakpoints)/configurationDone. Trace appears in the **DAP** tab. Linux uses gdb; Windows uses lldb when installed. See [RFC 0023](../rfcs/0023-dap-mvp.md).

### 9. Proof bundle

**Proof Bundle** exports `proof-bundle/` under the build directory with `capabilities.json`, `ir.json`, `abi.json`, NASM, and binary — same as `hla64 build --proof-bundle`.

The **Capabilities** tab shows a summary (`filesystemAccess`, syscalls, externs). **Toolchain** shows auto-detected WSL/linker status.

## Release build (optional)

```powershell
.\scripts\publish-assembly-lab.ps1 -Rids win-x64,linux-x64
```

GitHub tag releases also attach `assembly-lab-win-x64.zip` and `assembly-lab-linux-x64.tar.gz`.

## CLI equivalents

| Lab action | CLI |
|------------|-----|
| Live compile | `hla64 explain file.hla64` |
| Build | `hla64 build file.hla64 --source-map` |
| Run | `hla64 run file.hla64` |
| Proof bundle | `hla64 build file.hla64 --proof-bundle` |
| Debug | `hla64 debug --stdio` |

## Agent integration

Lab actions map to MCP tools (`compile`, `build`, `run`, `explain`). The **Agent** tab uses in-process `ExplainAgentService` (no MCP server spawn). External MCP stdio client from the Lab UI is deferred. See [04-mcp-agent.md](04-mcp-agent.md).

## Troubleshooting

| Issue | Fix |
|-------|-----|
| NASM not found | Install NASM; run `hla64 doctor` |
| Linux ELF on Windows | Install WSL + `gcc`; Lab run uses WSL when needed |
| Empty NASM tab | Fix diagnostics first; check target triple |
| No source map highlight | Run **Build** (source map emitted on build) |

## Next steps

- [Getting started](01-getting-started.md)
- [RFC 0024 — Assembly Lab](../rfcs/0024-assembly-lab.md)
- [development.md](../development.md)
