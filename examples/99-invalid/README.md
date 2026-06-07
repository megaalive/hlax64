# Invalid Examples (`examples/99-invalid/`)

Educational sources that **must fail to compile** (or fail to parse) with a specific diagnostic code. These mirror the automated conformance suite under `tests/conformance/invalid/` (21 cases).

## Purpose

- Show learners what *not* to do
- Lock expected error codes so the compiler keeps producing actionable diagnostics
- Give agents a catalog of failure modes (bad ABI, bad types, missing return, etc.)

## Catalog (complete)

| Example | Code / kind | Notes |
|---------|-------------|-------|
| `address-of-register` | HLAX0023 | `mov(&rax, …)` |
| `array-unsupported-type` | HLAX0020 | array of unknown type |
| `const-divide-by-zero` | HLAX0032 | compile-time `1 / 0` |
| `enum-duplicate-member` | HLAX0039 | duplicate enum member |
| `enum-undefined-member` | HLAX0041 | missing enum member |
| `expr-divide-by-zero` | HLAX0038 | runtime `1 / 0` assignment |
| `expr-invalid-target` | HLAX0035 | assign into non-assignable target |
| `index-non-array` | HLAX0026 | index on non-array |
| `memory-ref-literal` | parse error | `mov([42], rax)` |
| `mismatched-program-name` | parse error | missing `program` header |
| `missing-return` | HLAX0062 | verification on empty `@returns` body |
| `record-unknown-field` | HLAX0043 | unknown record field |
| `static-duplicate` | HLAX0045 | duplicate static symbol |
| `unclosed-program` | parse error | missing `end` |
| `unknown-extern-call` | HLAX0054 | call to undeclared procedure |
| `unknown-instruction` | HLAX0071 | `movz` |
| `unknown-param-type` | HLAX0020 | unknown parameter type |
| `unknown-register` | HLAX0012 | `raxz` |
| `unknown-type` | HLAX0020 | unknown variable type |
| `variadic-extern` | HLAX0055 | variadic `printf` + float64 |
| `wrong-operand-count` | HLAX0004 | `mov(rax)` |

Automated: `ExamplesInvalidTests` reads each folder's `manifest.json` + optional `expected-codes.txt`.

```powershell
dotnet test --filter ExamplesInvalidTests
hla64 test tests/conformance/invalid
```
