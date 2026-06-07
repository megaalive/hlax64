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

---

## Quick start — `hello.hla64` (step by step)

This walkthrough matches opening `examples/00-getting-started/hello.hla64`.

### Step 1 — Open the file

1. Click **Open File** for examples like `hello.hla64`
2. Click **Open Project** for folders with `hla64.toml` (created via `hla64 new console`)
3. **Save** / **Ctrl+S** / **Save As** persist edits to disk

### Step 2 — Target triple

Combo **Target** (toolbar):

- `windows-x64-msabi` — native Windows `.exe` (needs `lld-link` / LLVM)
- `linux-x64-sysv` — Linux ELF (on Windows, **Run** uses WSL if installed)

Check **Toolchain** tab: confirms NASM, Windows linker, WSL.

### Step 3 — Live compile (automatic)

Edit the **Source** tab. After ~400 ms debounce:

- Left **Diagnostics** panel updates
- **IR**, **NASM**, **ABI** tabs refresh

You do **not** need to click anything for live compile.

### Step 4 — Build / Run / Proof bundle

By default **Build**, **Run**, and **Proof Bundle** are **enabled immediately**.

Optional **Strict plan gate** (checkbox, default **OFF**):

- When **ON**: after each edit you must check **Plan approved** before Build/Run/Proof (agent safety workflow)
- When **OFF** (default): build anytime; **Plan** tab is still useful to preview steps

1. Click **Build**
2. Read **Output** tab — shows paths to `.nasm`, `.hlamap.json`, and binary (if link succeeded)
3. Click **Open Build Folder** to open `build\hello` in Explorer

**Where is `hello.exe`?**

```
examples/00-getting-started/build/hello/
  hello.nasm
  hello.obj          ← always after successful assemble
  hello.hlamap.json
  hello.exe          ← only if linking succeeded (Windows target + linker found)
  proof-bundle/      ← after Proof Bundle (may be compile-only)
```

If you see `.obj` but **no `.exe`**, linking failed. Common on Windows:

- LLVM/`lld-link` not installed or not on PATH
- Fix: install LLVM (`winget install LLVM.LLVM`) and run `scripts/setup-toolchain-path.ps1`
- **Output** tab explains compile-only vs full build
- **Proof Bundle** can still succeed in **compile-only** mode (see `proof-bundle/build.json` → `"compileOnly": true`)

### Step 5 — Run

**Run** = Build + execute. Stdout/stderr and exit code appear in **Output**.

### Step 6 — Other tabs

| Tab | Purpose |
|-----|---------|
| **Plan** | Steps the compiler will take (emit → assemble → link) |
| **Diff** | Semantic diff vs file content when opened |
| **Agent** | **Explain** → diagnostics + `suggestedFix`; **Apply Fix** patches source |
| **MCP** | In-process MCP client to `HlaX64.McpServer` (Start / Tools / Explain) |
| **DAP** | **Debug** session trace |
| **Disasm** | NASM + objdump when binary exists |

---

## FAQ (common confusion)

### Build/Run disabled until I check Plan approved?

**Default (Strict plan gate OFF):** buttons should be enabled. If you enabled **Strict plan gate**, uncheck it or check **Plan approved** after reviewing the **Plan** tab.

### Flash or flicker while typing?

Previously the app refreshed plan/capabilities and toggled plan approval on **every keystroke** — fixed: heavy work is debounced (~400 ms). If you still see flicker when debugging from Visual Studio, try running Lab standalone: `dotnet run --project src/HlaX64.AssemblyLab`.

### Open File vs Open Folder looks the same?

Both load a `.hla64` into the editor. Difference is context:

- **Open File** → one file, status: `Single file: …`
- **Open Folder** → project scan, status: `Project: … (N files)` or `Folder: … (first of N, no hla64.toml)`

### No `.exe` after Build?

Link step failed. You still get NASM + `.obj` under `build/<name>/`. See **Toolchain** tab and **Output** tab for the linker message.

---

## Workflow reference

### Pipeline tabs

| Tab | Shows |
|-----|-------|
| **IR** | Lowered intermediate representation |
| **NASM** | Emitted assembly |
| **Disasm** | NASM listing with source-map columns; objdump when binary exists |
| **ABI** | Lowered functions, stack frames, verification hints |

### Source map sync

After **Build**, double-click a diagnostic. **NASM** tab highlights the line (`>>>` prefix).

### Debug

**Debug** builds, spawns `hla64 debug --stdio`, sets breakpoints from gutter. Linux: gdb; Windows: lldb.

### Proof bundle

Same as `hla64 build --proof-bundle`. Exports under `build/<name>/proof-bundle/`.

---

## Release build (optional)

```powershell
.\scripts\publish-assembly-lab.ps1 -Rids win-x64,linux-x64
```

---

## CLI equivalents

| Lab action | CLI |
|------------|-----|
| Live compile | `hla64 explain file.hla64` |
| Build | `hla64 build file.hla64 --source-map` |
| Run | `hla64 run file.hla64` |
| Proof bundle | `hla64 build file.hla64 --proof-bundle` |
| Debug | `hla64 debug --stdio` |

---

## Troubleshooting

| Issue | Fix |
|-------|-----|
| NASM not found | Install NASM; run `hla64 doctor` |
| No `.exe`, only `.obj` | Install/link LLVM `lld-link`; see **Toolchain** tab |
| Linux ELF on Windows | Install WSL + `gcc`; Lab run uses WSL when needed |
| Empty NASM tab | Fix diagnostics first; check target triple |
| No source map highlight | Run **Build** (source map emitted on build) |
| MCP hang | Rebuild Lab; MCP uses single stdout reader (see development.md) |

---

## Panduan singkat (Bahasa Indonesia)

1. **Buka file** → Open File → pilih `hello.hla64`
2. **Target** → `windows-x64-msabi` untuk `.exe` di Windows
3. **Build** → lihat tab **Output** → **Open Build Folder**
4. Jika tidak ada `.exe`, baca pesan linker di Output; pasang LLVM atau gunakan WSL + target Linux
5. **Strict plan gate** default mati — Build/Run langsung bisa dipakai tanpa centang Plan

---

## Next steps

- [Getting started](01-getting-started.md)
- [RFC 0024 — Assembly Lab](../rfcs/0024-assembly-lab.md)
- [development.md](../development.md)
