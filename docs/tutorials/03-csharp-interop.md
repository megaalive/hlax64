# Tutorial 3 — C# Interop

Export HlaX64 procedures from a shared library and call them from C# via P/Invoke.

## 1. Export a library

Example: `examples/interop/07-interop/export-lib.hla64` defines exported procedures for a `.so` / `.dll`.

Build a shared library (Linux):

```bash
hla64 build examples/interop/07-interop/export-lib.hla64 \
  --output-kind shared-library \
  -o build/libexport.so
```

Inspect exports:

```bash
hla64 explain examples/interop/07-interop/export-lib.hla64
```

## 2. Generate a C header

```bash
hla64 generate-header examples/interop/07-interop/export-lib.hla64 -o build/export_lib.h
```

Use the header from C/C++ if you prefer raw `dlopen` / `LoadLibrary`.

## 3. Generate C# P/Invoke

```bash
hla64 generate-pinvoke examples/interop/07-interop/export-lib.hla64 -o build/ExportLib.cs
```

The generator emits `[DllImport]` attributes with calling convention and entry point names matching the NASM export labels.

## 4. Call from C#

Minimal consumer (adjust library name for your OS):

```csharp
// Program.cs — place next to generated ExportLib.cs
using System;
using System.Runtime.InteropServices;

class Program
{
    static void Main()
    {
        // NativeMethods from generated ExportLib.cs
        var result = ExportLib.AddTwo(10, 20);
        Console.WriteLine($"10 + 20 = {result}");
    }
}
```

Build and run (Linux):

```bash
dotnet new console -o interop-demo -f net10.0
cp build/ExportLib.cs interop-demo/
cp build/libexport.so interop-demo/
# merge NativeMethods into Program.cs or partial class
cd interop-demo && dotnet run
```

Ensure `libexport.so` is on `LD_LIBRARY_PATH` or copied next to the managed assembly.

## 5. ABI checklist

| Check | Command |
|-------|---------|
| Target triple | `--target linux-x64-sysv` or `windows-x64-msabi` |
| Output kind | `--output-kind shared-library` |
| Symbol names | `hla64 explain` → export labels |
| Integer width | HlaX64 `int64` ↔ C# `long` |

Run `hla64 explain-abi` for register/stack rules on your target.

## 6. MCP workflow for agents

An agent can:

1. `generate-pinvoke` — emit C# bindings
2. `build` with `outputKind: shared-library`
3. `run` a test harness manifest

See [04-mcp-agent.md](04-mcp-agent.md).

## Related

- Integration sample: `tests/samples/export_lib/`
- [docs/compatibility.md](../compatibility.md)
