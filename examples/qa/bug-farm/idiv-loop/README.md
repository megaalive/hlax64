# idiv-loop — regression harness for signed `idiv` / `mod` in loops

These programs isolate Lehmer-style division chains used heavily in Project Euler.

| Program | Expected exit | Notes |
|---------|---------------|-------|
| `idiv-loop.hla64` | 60 | 10× `(32319 / 5040)` |
| `mod-only.hla64` | 2079 | `32319 % 5040` |
| `static-store.hla64` | 362880 | static `int64[10]` qword store/load |
| `lehmer-indices.hla64` | 26 | sum of Lehmer indices for k=999999 |

Run (Windows):

```powershell
dotnet run --project src/HlaX64.Cli -- build examples/qa/bug-farm/idiv-loop/static-store.hla64 --target windows-x64-msabi
```

Automated: `dotnet test tests/HlaX64.Compiler.Tests --filter FullyQualifiedName~IdivLoopTests`
