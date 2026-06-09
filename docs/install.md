# Install HlaX64

The recommended path is **Assembly Lab**. It bundles the desktop app, `hla64` CLI, MCP server, runtime files, docs, examples, and app-local wrapper scripts so users can install, open a `.hla64` file, and build/run without manually wiring project paths.

HlaX64 targets **windows-x64-msabi** and **linux-x64-sysv**.

## Option 1 — Assembly Lab (recommended)

1. Open [Releases](https://github.com/megaalive/hlax64/releases).
2. Download:
   - `assembly-lab-win-x64.zip` on Windows
   - `assembly-lab-linux-x64.tar.gz` on Linux
3. Extract it.
4. Run Assembly Lab:

**Windows (PowerShell):**

```powershell
Expand-Archive assembly-lab-win-x64.zip -DestinationPath HlaX64
.\HlaX64\HlaX64.AssemblyLab.exe
```

Or install to the user profile and create a Start Menu shortcut:

```powershell
.\HlaX64\install.ps1
```

**Linux:**

```bash
mkdir -p HlaX64
tar xzf assembly-lab-linux-x64.tar.gz -C HlaX64
./HlaX64/HlaX64.AssemblyLab
```

Or install to `~/.local/share/hlax64/assembly-lab` and create a desktop entry:

```bash
sh ./HlaX64/install.sh
```

Inside Assembly Lab, open the **Settings** tab and press **Test** or **Auto Detect**. The embedded terminal can run:

```bash
hla64 doctor
hla64 build examples/curriculum/00-getting-started/hello.hla64
```

The Assembly Lab bundle contains:

- `HlaX64.AssemblyLab`
- `cli/` with `HlaX64.Cli`
- `mcp/` with `HlaX64.McpServer`
- `runtime/` with HlaX64 runtime NASM files
- `examples/` and `docs/`
- `hla64.cmd` / `hla64.sh` wrapper scripts
- `bundle-manifest.json`

No global `PATH` change is required for basic Assembly Lab usage.

## Toolchain Status

Assembly Lab and `hla64 doctor` check the same toolchain:

| Tool | Why it is needed | Windows | Linux |
|------|------------------|---------|-------|
| [.NET 10 runtime](https://dotnet.microsoft.com/download/dotnet/10.0) | Runs framework-dependent releases | Required unless using future self-contained installer | Required unless using future self-contained package |
| [NASM](https://nasm.us) | Assembles generated NASM | Bundled if available, else install with `winget install -e --id NASM.NASM` | Bundled if available, else `sudo apt install nasm` |
| Linker | Produces executable | `lld-link`/LLVM preferred, MSVC `link.exe` also supported | `gcc` or `ld` |
| WSL | Optional Linux target from Windows | Optional for `linux-x64-sysv` from Windows | Not applicable |

Resolution order is:

1. Assembly Lab Settings paths
2. App-local bundled tools
3. Environment variables (`HLA64`, `HLAX64_RUNTIME_DIR`, `NASM`, `LLD_LINK`, `HLAX64_LINUX_LINKER`)
4. `PATH`
5. Common OS locations and WSL probes

## Option 2 — CLI archive (advanced)

Use this if you only want the command line tools.

1. Open [Releases](https://github.com/megaalive/hlax64/releases) and download:
   - `hla64-linux-x64.tar.gz` or `hla64-win-x64.zip`
2. Verify checksum against `checksums.txt`.
3. Extract and run the wrapper or add the folder to `PATH`.

**Linux:**

```bash
tar xzf hla64-linux-x64.tar.gz
export PATH="$PWD:$PATH"
hla64 --version
hla64 doctor
```

**Windows (PowerShell):**

```powershell
Expand-Archive hla64-win-x64.zip -DestinationPath HlaX64Cli
$env:PATH = "$PWD\HlaX64Cli;$env:PATH"
hla64 --version
hla64 doctor
```

Archives contain the CLI, runtime files, MCP server under `mcp/`, and `LICENSE`.

## Option 3 — .NET global tool

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

## Option 4 — Run from clone (developers)

```bash
git clone https://github.com/megaalive/hlax64.git
cd hlax64
dotnet build
dotnet test
dotnet run --project src/HlaX64.Cli -- --version
dotnet run --project src/HlaX64.Cli -- run examples/curriculum/00-getting-started/hello.hla64
```

**Bootstrap scripts** (verify toolchain before first build):

```powershell
# Windows
.\scripts\install-windows.ps1
```

```bash
# Linux / WSL
sh ./scripts/install-linux.sh
```

Both scripts exit non-zero when required tools are missing and run `hla64 doctor` when prerequisites are present.

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

More: [patterns.md](patterns.md) (common idioms & build failures), [diagnostics.md](diagnostics.md), [compatibility.md](compatibility.md).
