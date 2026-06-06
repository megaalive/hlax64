# Interop

Build a shared library and generate C / C# bindings:

```bash
hla64 build examples/07-interop/export-lib.hla64 --output-kind shared-library -o build/export-lib
hla64 generate-header examples/07-interop/export-lib.hla64 -o build/export-lib/export_lib.h
hla64 generate-pinvoke examples/07-interop/export-lib.hla64 -o build/export-lib/ExportLib.cs
```

See also `tests/samples/export_lib/` for CI-tested interop output.
