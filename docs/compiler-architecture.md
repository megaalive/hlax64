# HlaX64 — Compiler Architecture

> **Status**: Draft · bagian dari **Fase 9.5 — Stabilisasi Arsitektur**
> **Lihat juga**: [`HlaX64_Project_Plan.md` §6](../HlaX64_Project_Plan.md) ·
> [`HlaX64_Technical_Review_and_Next_Priorities.md` §3.3](../HlaX64_Technical_Review_and_Next_Priorities.md) ·
> [`docs/runtime-contract.md`](./runtime-contract.md)

Dokumen ini menjelaskan pipeline kompilasi HlaX64 setelah Fase 9.5
stabil. Sebelum stabilisasi, pipeline langsung `AST → NASM emitter`.
Setelahnya, AST diturunkan dulu ke **Intermediate Representation (IR)**
yang netral, lalu IR dilewatkan ke **ABI lowerer**, dan akhirnya ke
backend.

---

## 1. Pipeline tingkat tinggi

```text
.hla64 source
  ↓ Lexer
Tokens
  ↓ Parser
Ast
  ↓ Binding + Type checking
Bound Tree
  ↓ Lowering
Ir (netral, SSA-like opsional)
  ↓ IAbiLowerer (SysV / MicrosoftX64)
Lowered Function
  ↓ NasmEmitter (atau MASM / LLVM)
.asm / .ll
  ↓ nasm / clang
.o
  ↓ ld / link
executable / shared library / DLL
  ↓ Native integration test
asserted output & exit code
```

---

## 2. Tahap demi tahap

### 2.1 Lexer

Mengubah source `.hla64` menjadi stream token.

- Keyword (`program`, `procedure`, `begin`, `end`, `if`, `else`, `endif`,
  `while`, `endwhile`, `var`, `mov`, `add`, `call`, `ret`, …).
- Identifier, integer literal, string literal, register.
- Posisi (line, column) untuk diagnostics.
- Skip whitespace dan komentar (`//`, `/* … */`).

### 2.2 Parser (Recursive descent)

Mengubah token menjadi AST.

Node penting: `ProgramNode`, `BlockNode`, `ProcedureNode`, `CallNode`,
`InstructionNode`, `LiteralNode`, `RegisterNode`, `IdentifierNode`,
`IfNode`, `WhileNode`, `VarNode`.

### 2.3 Binding + Type Checking

Membangun **bound tree** dengan:

- Symbol table (scope chain).
- Type resolution (identifier → `IntegerTypeSymbol` dengan `BitWidth` & `IsSigned`).
- Validasi: jumlah operand, jenis operand, register valid, instruksi valid,
  identifier terdeklarasi, duplikat procedure, narrowing conversion.

### 2.4 Lowering AST → IR

AST/bound tree diturunkan ke **IR** (lihat §3). IR adalah representasi
tunggal yang netral terhadap syntax dan ABI.

### 2.5 ABI Lowering (`IAbiLowerer`)

IR dilewatkan ke implementasi `IAbiLowerer` yang sesuai target:

- `SysVAbiLowerer` (Linux x64 SysV, MVP).
- `MicrosoftX64AbiLowerer` (Windows MS-ABI, Fase 11).

Tanggung jawab: argument register assignment, return register,
callee-saved preservation, stack frame, stack alignment, call lowering,
scratch registers.

### 2.6 Backend (NASM / MASM / LLVM)

`NasmEmitter` hanya bertugas **memformat** `LoweredFunction` menjadi
teks NASM. Tidak membaca AST, tidak memutuskan signedness, tidak
menghitung ABI placement.

### 2.7 Toolchain

`nasm -f elf64` (Linux) atau `nasm -f win64` (Windows), lalu
`gcc`/`ld`/`lld-link` untuk menghasilkan executable / shared library /
DLL.

### 2.8 Native integration test

Fixture menjalankan: tulis source → compile → nasm → link → run binary
→ cek stdout / stderr / exit code. Lihat
[`HlaX64_Project_Plan.md` §13.5](../HlaX64_Project_Plan.md).

---

## 3. Intermediate Representation (IR v0.1)

### 3.1 Struktur

```text
IrFunction
  ├─ Name
  ├─ Parameters : list<IrValue>
  ├─ ReturnType : IntegerTypeSymbol
  └─ BasicBlocks : list<IrBasicBlock>

IrBasicBlock
  ├─ Label
  └─ Instructions : list<IrInstruction>
  └─ Terminator : Branch | ConditionalBranch | Return

IrValue
  ├─ Kind : Local | Constant | Parameter
  ├─ Type : IntegerTypeSymbol
  └─ Name / Value

IrInstruction
  ├─ Op
  └─ Operands : list<IrValue>
```

### 3.2 Opcode v0.1

| Op | Operand | Deskripsi |
|----|---------|-----------|
| `LoadConstant` | dst, value | muat konstanta |
| `LoadLocal` | dst, local | muat nilai variabel lokal |
| `StoreLocal` | local, src | simpan ke variabel lokal |
| `Move` | dst, src | salin register/nilai |
| `Add` | dst, a, b | a + b |
| `Subtract` | dst, a, b | a − b |
| `Multiply` | dst, a, b | a × b |
| `Divide` | dst, a, b | a ÷ b (signed/unsigned ikut tipe) |
| `Compare` | dst, a, b, kind | hasil compare, kind signed/unsigned |
| `Branch` | label | jump unconditional |
| `ConditionalBranch` | cond, t, f | jump bersyarat |
| `Call` | dst?, target, args | panggil fungsi |
| `Return` | value? | kembali dari prosedur |

### 3.3 `CompareKind`

```text
Equal
NotEqual
LessThanSigned       LessThanUnsigned
LessOrEqualSigned    LessOrEqualUnsigned
GreaterThanSigned    GreaterThanUnsigned
GreaterOrEqualSigned GreaterOrEqualUnsigned
```

CompareKind **wajib** dibawa sampai ke emitter agar jump instruction
benar (`jl` vs `jb`).

### 3.4 Contoh IR (AddTwo)

```text
function AddTwo(a: i64, b: i64) -> i64 {
  block entry:
    v0 = LoadParameter a
    v1 = LoadParameter b
    v2 = Add v0, v1
    Return v2
}
```

---

## 4. ABI Lowering — kontrak SysV v0.1

```text
Argument registers (urutan kiri-ke-kanan):
  arg1=rdi, arg2=rsi, arg3=rdx, arg4=rcx, arg5=r8, arg6=r9
  lebih dari 6 → spill ke stack (callee harus align ke 16 byte)

Return: rax (untuk tipe 64-bit), rax+rdx (untuk struct kecil)

Caller-saved: rax rcx rdx rsi rdi r8 r9 r10 r11
Callee-saved: rbx rbp r12 r13 r14 r15

Stack alignment pada saat call: RSP ≡ 0 (mod 16)
```

Lowerer **tidak** menerima AST; hanya IR + `CompilationOptions`. Output
adalah `LoweredFunction` yang siap di-emit.

---

## 5. `CompilationOptions` (pusat)

```csharp
public sealed record CompilationOptions(
    TargetTriple Target,             // linux-x64-sysv, windows-x64-msabi
    OutputKind OutputKind,           // Executable | ObjectFile | StaticLibrary
                                      // | SharedLibrary | AssemblyOnly
    RuntimeMode RuntimeMode,         // Library (default) | Inline
    OptimizationLevel Optimization,  // None | Debug | Release
    bool EmitDebugInfo);
```

- CLI memetakan argumen → `CompilationOptions`.
- Kombinasi tidak didukung ⇒ diagnostic jelas.
- Tidak ada string "target" tersebar di kode.

---

## 6. Batasan arsitektur saat ini

> **Sebelum Fase 9.5**: alur langsung `AST → NASM emitter`. Emitter
> memutuskan signedness, ukuran operand, ABI placement, dan entry
> point. Risiko: emitter dipenuhi `if (target == ...) …`.

> **Setelah Fase 9.5**: semua keputusan semantic dan ABI pindah ke IR
> + `IAbiLowerer`. Backend hanya memformat.

---

## 7. 7 Workstream stabilisasi

| WS | Nama | Output | DoD utama |
|----|------|--------|-----------|
| A | Compilation model | `CompilationOptions`, `TargetTriple`, `OutputKind`, `RuntimeMode` | CLI memetakan argumen → options |
| B | Semantic type system | `IntegerTypeSymbol` (`BitWidth`, `IsSigned`) | implicit narrowing ditolak |
| C | IR | `IrFunction`, `IrBasicBlock`, `IrInstruction` | snapshot test IR ada |
| D | ABI lowering | `IAbiLowerer` + `SysVAbiLowerer` | call 0–6 args lulus, stack align lulus |
| E | Backend NASM refactor | `NasmEmitter` dari `LoweredFunction` | emitter tidak baca AST |
| F | Native integration tests | fixture build+run | 16 sample native test di Linux CI |
| G | Dokumentasi | `compiler-architecture.md`, `runtime-contract.md`, sync README | semua doc saling rujuk |

Detail per workstream lihat
[`HlaX64_Project_Plan.md` §9.5](../HlaX64_Project_Plan.md).
