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
- Runtime `:=` expressions (RFC 0004 Sprint 2): int64 locals/registers, HLAX0035–38; eval clobbers `rax`/`rbx`
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

- `CompilationOptions` centralizes target, output kind, runtime mode, `--optimize O0|O1|O2`, `--cpu` / `--features`
- CLI, MCP, and LSP share the same `Compilation` entry point
- LSP: diagnostics, hover, completion, definition, references, signatureHelp, semanticTokens, format codeAction
- Native integration tests: compile → assemble → link → run
- Verification: `verify-stack`, `verify-abi`; proof bundle; `hla64 diff` / `plan`

---

## Experimental

| Area | Notes |
|------|-------|
| LSP virtual documents | IR/NASM/stack via executeCommand — read-only, no live debug session |
| Source maps / DWARF | Sidecar JSON with lookup; Linux `%line` + file table stub; Windows PDB deferred |
| O2 optimizer | Copy propagation + xor-zero peephole; no global DCE or assisted regalloc |
| CPU / SIMD | AVX2 YMM lowering; `simd.*` + `atomic.*` intrinsics MVP (RFC 0019/0020 partial) |
| Packages | Path deps + lock enforcement; git deps when git on PATH |
| DAP | Linux gdb backend via `HlaX64.DebugAdapter`; Windows/lldb deferred |
| Variadic | SysV `printf` integer+cstring; float variadic still HLAX0055 |
| Proof bundle | Static capability manifest + optional tests.json; not formal verification |
| Bounds warnings | Literal / const indices only |
| Windows backend | CI smoke tests; fewer native run manifests than Linux |

---

## Planned (post-hardening)

| Area | Notes |
|------|-------|
| Phase 15 GUI | Avalonia/WPF Assembly Lab — skipped until cross-platform decision |
| Assisted regalloc | `--register-mode assisted` |
| Full DWARF/PDB | Debuggers on Windows; live trace sink |
| Package resolver | Version ranges, transitive deps, registry |
| Full DAP | Windows lldb; variables/evaluate; full GDB MI |
| Variadic printf | Windows MS ABI + float variadic in AL/xmm |
| Differential run on Windows | WSL or native link in CI |

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
