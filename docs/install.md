# Install HlaX64

Download a pre-built release or install from source. HlaX64 targets **linux-x64-sysv** (default) and **windows-x64-msabi**.

## Prerequisites

| Tool | Linux | Windows |
|------|-------|---------|
| [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | Required to run `hla64` | Required |
| [NASM](https://nasm.us) | `apt install nasm` | Installer or `choco install nasm` |
| Linker | `gcc` or `ld` | `lld-link` (LLVM) or MSVC `link.exe` |

Verify with:

```bash
hla64 doctor
```

## Option 1 — Release archive (recommended)

1. Open [Releases](https://github.com/megaalive/hlax64/releases) and download:
   - `hla64-linux-x64.tar.gz` or `hla64-win-x64.zip`
2. Verify checksum against `checksums.txt` in the same release.
3. Extract and add the folder to your `PATH`.

**Linux:**

```bash
tar xzf hla64-linux-x64.tar.gz
export PATH="$PWD:$PATH"
hla64 --version
hla64 run examples/00-getting-started/hello.hla64
```

**Windows (PowerShell):**

```powershell
Expand-Archive hla64-win-x64.zip -DestinationPath .
$env:PATH = "$PWD\win-x64;$env:PATH"
hla64 --version
```

Archives contain the CLI, MCP server under `mcp/`, and `LICENSE`.

## Option 2 — .NET global tool

Requires the .NET 10 SDK (not just runtime):

```bash
git clone https://github.com/megaalive/hlax64.git
cd hlax64
dotnet pack src/HlaX64.Cli/HlaX64.Cli.csproj -c Release
dotnet tool install --global --add-source ./src/HlaX64.Cli/bin/Release HlaX64.Cli
hla64 --version
```

Update:

```bash
dotnet tool update --global HlaX64.Cli
```

Uninstall:

```bash
dotnet tool uninstall --global HlaX64.Cli
```

## Option 3 — Run from clone (developers)

```bash
git clone https://github.com/megaalive/hlax64.git
cd hlax64
dotnet build
dotnet run --project src/HlaX64.Cli -- --version
dotnet run --project src/HlaX64.Cli -- run examples/00-getting-started/hello.hla64
```

See [development.md](development.md) for tests, LSP, and MCP setup.

## Uninstall

- **Release archive:** delete the extracted folder and remove it from `PATH`.
- **Global tool:** `dotnet tool uninstall --global HlaX64.Cli`.
- **Clone:** delete the repository directory.

## Troubleshooting

| Problem | Action |
|---------|--------|
| `nasm: not found` | Install NASM; re-run `hla64 doctor` |
| Link errors on Linux | Install `gcc`; ensure 64-bit toolchain |
| Windows link errors | Install LLVM (`lld-link`) or Visual Studio Build Tools |
| WSL from Windows host | Run `hla64 doctor` — CLI detects WSL when appropriate |

More: [diagnostics.md](diagnostics.md), [compatibility.md](compatibility.md).
