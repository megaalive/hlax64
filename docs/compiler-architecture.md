# HlaX64 — Compiler Architecture

> **Architecture as of compiler 0.1.x** · Draft  
> **See also:** [`architecture.md`](./architecture.md) · [`runtime-matrix.md`](./runtime-matrix.md) · [`runtime-contract.md`](./runtime-contract.md)

---

## Status legend

| Section | Meaning |
|---------|---------|
| **Implemented** | Ships in HlaX64 0.1.x; covered by tests |
| **Experimental** | Partial or MVP; API/syntax may change |
| **Planned** | Specified or roadmapped; not in the compiler yet |

---

## Implemented

### Pipeline (0.1.x)

```text
.hla64 source
  ↓ Lexer
Tokens
  ↓ Parser
AST
  ↓ Semantic analysis (+ compile-time const eval)
Bound program
  ↓ AstToIrLowering
IR (IrFunction / IrBasicBlock / IrInstruction)
  ↓ IAbiLowerer (SysV or Microsoft x64)
LoweredFunction
  ↓ NasmEmitter
.asm
  ↓ nasm + gcc/ld (or lld-link)
executable / shared library / DLL
```

### Lexer & parser

- Keywords: `program`, `procedure`, `begin`, `end`, `if`, `while`, `var`, `const`, `export`, …
- Registers, integer / hex (`$FF`) literals, strings
- Source locations for diagnostics
- `//` and `/* */` comments

### Semantic analysis

- Symbol tables (procedure scope, parameters, locals)
- Integer type registry (`int64`, `uint64`, `byte`, `ptr`, …)
- Instruction / register validation, narrowing checks
- Compile-time `const` blocks (RFC 0004): expression evaluation, HLAX0031–34
- Optional bounds warnings (HLAX0030)

### IR (v0.1)

Opcodes include `Move`, `Add`, `Subtract`, `Multiply`, `Divide`, `Compare`, `Branch`, `ConditionalBranch`, `Call`, `Return`, `LoadConstant`.

### ABI lowering

- **SysV** (Linux): `rdi, rsi, rdx, rcx, r8, r9` for integer args; `rax` return
- **Microsoft x64**: `rcx, rdx, r8, r9`; 32-byte shadow space; `rax` return
- Stack frame layout for locals and `type[N]` arrays

### Backend

- `NasmEmitter` formats `LoweredFunction` only — no AST access
- Operand order: HLA `mov(src, dest)` → NASM `mov dest, src`

### Tooling integration

- `CompilationOptions` centralizes target, output kind, runtime mode
- CLI, MCP, and LSP share the same `Compilation` entry point
- Native integration tests: compile → assemble → link → run

---

## Experimental

| Area | Notes |
|------|-------|
| LSP | Diagnostics, hover, completion, go-to-definition, symbols, format-on-save — not full semantic IDE |
| Bounds warnings | Literal / const indices only; no range analysis for register indices |
| Windows backend | CI smoke tests; fewer native run manifests than Linux |
| Fuzz coverage | Early parser/lexer fuzz; not exhaustive |

---

## Planned

| Area | Target phase | Notes |
|------|--------------|-------|
| Runtime expressions `:=` | 16 | RFC 0004 Sprint 2 |
| Struct, `sizeof`, `alignof` | 16 | Audit §5.4 |
| Stack arguments >6 (SysV) / >4 (Windows) | 17 | ABI completion |
| DWARF / source map | 19 | Debug visibility |
| Optimizer (DCE, const fold at IR) | 20 | After verification pass |
| SIMD / intrinsics | 21 | After instruction DB |
| Modules / packages | 22 | Project manifest |

> Features listed as “future” in older docs (IR pipeline, Windows ABI, shared libraries, MCP) are **implemented** in 0.1.x unless they appear in the Planned table above.

---

## IR sketch (implemented)

```text
IrFunction
  ├─ Name, Parameters, ReturnType
  └─ BasicBlocks[]
       └─ IrInstruction { Op, Operands, CmpKind?, Immediate? }
```

Example:

```text
function AddTwo(a, b) {
  entry:
    v2 = Add a, b
    Return v2
}
```

---

## CompilationOptions (implemented)

```csharp
CompilationOptions(
    TargetTriple Target,       // linux-x64-sysv | windows-x64-msabi
    OutputKind OutputKind,     // Executable | SharedLibrary | …
    RuntimeMode RuntimeMode,   // Library | Inline
    OptimizationLevel Optimization,
    bool EmitDebugInfo,
    CompilerWarnings Warnings);
```

---

## Historical note

Before **Fase 9.5**, the backend emitted NASM directly from the AST. As of **0.1.x**, all ABI and semantic lowering goes through IR + `IAbiLowerer`; the NASM emitter is a formatter only.
