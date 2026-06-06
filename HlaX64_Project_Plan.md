# HLA-X64 Inspired Project Plan

> **Working title:** `HlaX64`  
> **Tagline:** *AI-friendly x64 assembly layer for executable vibe coding*  
> **Main idea:** Membuat bahasa assembly x64 yang lebih ramah manusia dan AI, lalu mengubahnya menjadi NASM/MASM/LLVM-compatible output yang bisa dikompilasi, dites, di-benchmark, dan dipakai dari aplikasi modern.

---

## 1. Latar Belakang

Vibe coding mengubah cara programmer bekerja. Programmer tidak lagi selalu menulis setiap baris kode secara manual, tetapi memberi instruksi ke AI agent, lalu AI menghasilkan kode, menjalankan tool, membaca error, memperbaiki, dan mengulang proses sampai program berjalan.

Alur umum vibe coding saat ini:

```text
Natural language prompt
  ↓
AI coding agent
  ↓
High-level source code
  ↓
Build / test / debug
  ↓
Executable application
```

Namun masih ada masalah besar:

1. **High-level language semakin jauh dari mesin.**  
   Bahasa seperti Python, JavaScript, C#, Go, Rust, dan C++ sangat produktif, tetapi banyak detail CPU, ABI, register, memory layout, stack, dan calling convention disembunyikan.

2. **Langsung menghasilkan binary dari AI terlalu berisiko.**  
   Secara teori AI bisa menghasilkan machine code langsung, tetapi binary sulit dibaca, sulit diaudit, sulit di-debug, dan sulit diverifikasi.

3. **Assembly murni terlalu rendah untuk vibe coding biasa.**  
   NASM, MASM, GAS, dan raw x64 assembly kuat, tetapi syntax dan detailnya mudah membuat AI maupun manusia salah, terutama pada ABI, stack alignment, register clobber, dan platform differences.

4. **Diperlukan lapisan tengah.**  
   Lapisan tersebut harus cukup dekat ke CPU, tetapi tetap bisa dibaca, dijelaskan, diuji, dan digenerate oleh AI.

Karena itu, proyek ini mengusulkan bahasa **HLA-inspired x64**: bukan port penuh HLA lama, tetapi bahasa low-level baru yang terinspirasi dari HLA, dirancang khusus untuk era AI coding agent.

---

## 2. Visi Proyek

Membangun lapisan assembly x64 yang ramah AI dan manusia untuk menghasilkan native executable code secara lebih aman, transparan, dan terukur.

Visi jangka panjang:

```text
Prompt manusia
  ↓
AI agent
  ↓
HLA-inspired x64 source
  ↓
Compiler/transpiler
  ↓
NASM/MASM/LLVM output
  ↓
Executable / DLL / shared library
  ↓
Test + benchmark + explanation
```

Proyek ini bukan bertujuan menggantikan NASM/MASM, tetapi menjadi **lapisan intermediate yang lebih aman untuk vibe coding menuju machine code**.

---

## 3. Tujuan Utama

### 3.1 Tujuan teknis

1. Membuat bahasa HLA-inspired untuk x64.
2. Membuat compiler/transpiler berbasis C#.
3. Menghasilkan output NASM x64 sebagai backend pertama.
4. Mendukung ABI dasar Linux x64 terlebih dahulu.
5. Menambahkan Windows x64 ABI pada fase berikutnya.
6. Menyediakan standard library minimal untuk console I/O, string dasar, memory dasar, dan numeric conversion.
7. Menyediakan test runner dan benchmark runner.
8. Menyediakan MCP server agar bisa dipanggil oleh AI coding tools.
9. Menyediakan C# interop generator untuk native DLL/shared library.

### 3.2 Tujuan produk

1. Menjadi tool edukasi assembly x64 modern.
2. Menjadi sandbox low-level untuk AI coding agent.
3. Menjadi jembatan dari vibe coding ke executable native code.
4. Menjadi fondasi untuk AI Assembly Lab / IDEai plugin.
5. Menjadi portfolio compiler/tooling serius berbasis C#.

### 3.3 Tujuan non-teknis

1. Dokumentasi harus jelas sejak awal.
2. Setiap fase harus punya deliverable kecil yang bisa diuji.
3. Tidak mengejar kompatibilitas penuh dengan HLA lama.
4. Tidak membuat assembler object writer sendiri pada awal proyek.
5. Tidak membuat IDE dulu sebelum compiler CLI stabil.

---

## 4. Prinsip Desain

### 4.1 AI-friendly

Bahasa harus mudah digenerate oleh AI. Syntax harus eksplisit, konsisten, dan tidak terlalu banyak bentuk alternatif.

Contoh gaya yang diinginkan:

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

### 4.2 Human-readable

Kode harus mudah dibaca oleh manusia yang sedang belajar assembly.

Contoh:

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;
```

### 4.3 Backend-first, bukan assembler penuh

Tahap awal tidak membuat assembler penuh yang langsung menulis object file.

Alur awal:

```text
.hla64 source
  ↓
HlaX64 compiler
  ↓
.nasm output
  ↓
nasm + gcc/ld
  ↓
executable
```

### 4.4 Explicit ABI

Setiap target harus jelas ABI-nya.

Target awal:

```text
linux-x64-sysv
```

Target berikutnya:

```text
windows-x64-msabi
```

### 4.5 Verified output

Output AI tidak boleh langsung dipercaya. Tool harus mendukung:

```text
compile → run → test → benchmark → explain
```

### 4.6 Small core, growing library

Core language harus kecil. Fitur besar seperti macro system, object model, exception, dan compile-time language ditunda sampai fondasi kuat.

---

## 5. Batasan Scope Awal

### 5.1 Yang termasuk MVP

MVP hanya mendukung:

1. Linux x64 System V ABI.
2. NASM x64 backend.
3. CLI compiler.
4. Program sederhana.
5. Procedure sederhana.
6. Variabel lokal dasar.
7. Integer 8/16/32/64 bit.
8. Register x64 umum.
9. Instruksi dasar: `mov`, `add`, `sub`, `imul`, `idiv`, `xor`, `and`, `or`, `cmp`, `jmp`, `je`, `jne`, `jg`, `jl`, `call`, `ret`.
10. Control flow sederhana: `if`, `else`, `endif`, `while`, `endwhile`.
11. `stdout.put` minimal untuk string literal dan integer.
12. Snapshot test untuk output NASM.
13. Unit test untuk parser dan semantic analyzer.

### 5.2 Yang tidak termasuk MVP

Jangan dikerjakan pada MVP:

1. IDE GUI.
2. VS Code extension.
3. LSP.
4. Windows x64.
5. LLVM backend.
6. Macro system kompleks.
7. Full HLA compatibility.
8. Object-oriented features.
9. Exception handling.
10. OSDev/bare-metal target.
11. SIMD.
12. Optimizer kompleks.
13. Object writer sendiri.

---

## 6. Arsitektur Solusi

### 6.1 Struktur repository

```text
HlaX64/
├─ src/
│  ├─ HlaX64.Compiler/
│  │  ├─ Lexing/
│  │  ├─ Parsing/
│  │  ├─ Ast/
│  │  ├─ Semantic/
│  │  ├─ Ir/
│  │  ├─ Diagnostics/
│  │  └─ Compilation.cs
│  │
│  ├─ HlaX64.Backend.Nasm/
│  │  ├─ NasmEmitter.cs
│  │  ├─ LinuxSysVEmitter.cs
│  │  └─ NasmFormatting.cs
│  │
│  ├─ HlaX64.Runtime/
│  │  ├─ linux-x64/
│  │  │  ├─ startup.nasm
│  │  │  ├─ stdout.nasm
│  │  │  └─ conversion.nasm
│  │  └─ include/
│  │     └─ stdlib64.hhf
│  │
│  ├─ HlaX64.Cli/
│  │  ├─ Program.cs
│  │  ├─ Commands/
│  │  │  ├─ BuildCommand.cs
│  │  │  ├─ RunCommand.cs
│  │  │  ├─ TestCommand.cs
│  │  │  └─ BenchCommand.cs
│  │  └─ Toolchain/
│  │     ├─ NasmTool.cs
│  │     └─ LinkerTool.cs
│  │
│  ├─ HlaX64.Testing/
│  │  ├─ TestManifest.cs
│  │  └─ TestRunner.cs
│  │
│  ├─ HlaX64.Benchmarking/
│  │  └─ BenchmarkRunner.cs
│  │
│  └─ HlaX64.McpServer/
│     ├─ Tools/
│     │  ├─ CompileHlaTool.cs
│     │  ├─ RunProgramTool.cs
│     │  ├─ ExplainAbiTool.cs
│     │  └─ BenchmarkFunctionTool.cs
│     └─ Program.cs
│
├─ tests/
│  ├─ HlaX64.Compiler.Tests/
│  ├─ HlaX64.Backend.Nasm.Tests/
│  ├─ HlaX64.Cli.Tests/
│  └─ samples/
│     ├─ hello/
│     ├─ arithmetic/
│     ├─ loops/
│     └─ procedures/
│
├─ docs/
│  ├─ language-spec.md
│  ├─ abi-linux-x64.md
│  ├─ abi-windows-x64.md
│  ├─ roadmap.md
│  ├─ examples.md
│  └─ ai-agent-usage.md
│
├─ examples/
│  ├─ hello.hla64
│  ├─ add_two.hla64
│  ├─ count_bytes.hla64
│  └─ checksum.hla64
│
├─ scripts/
│  ├─ build.ps1
│  ├─ test.ps1
│  ├─ build.sh
│  └─ test.sh
│
├─ HlaX64.sln
├─ README.md
└─ LICENSE
```

---

## 7. Bahasa: Desain Awal

### 7.1 File extension

Gunakan extension:

```text
.hla64
```

Jangan memakai `.hla` agar tidak dikira kompatibel penuh dengan HLA lama.

### 7.2 Struktur program minimal

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

### 7.3 Procedure

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;
```

### 7.4 Type awal

```text
int8
int16
int32
int64
uint8
uint16
uint32
uint64
byte
word
dword
qword
ptr
```

### 7.5 Register awal

```text
rax rbx rcx rdx
rsi rdi rbp rsp
r8 r9 r10 r11 r12 r13 r14 r15

eax ebx ecx edx
esi edi ebp esp
r8d r9d r10d r11d r12d r13d r14d r15d

ax bx cx dx
al bl cl dl
```

### 7.6 Control flow awal

```hla
if(rax = 0) then
    stdout.put("zero", nl);
else
    stdout.put("not zero", nl);
endif;
```

```hla
while(rcx < rdx) do
    add(1, rcx);
endwhile;
```

### 7.7 Calling convention abstraction

Source boleh mendefinisikan target:

```hla
#pragma target("linux-x64-sysv")
```

Untuk Windows nanti:

```hla
#pragma target("windows-x64-msabi")
```

---

## 8. CLI Design

### 8.1 Build

```bash
hla64 build examples/hello.hla64
```

Output:

```text
build/hello/hello.nasm
build/hello/hello.o
build/hello/hello
```

### 8.2 Run

```bash
hla64 run examples/hello.hla64
```

### 8.3 Emit NASM only

```bash
hla64 emit-nasm examples/hello.hla64 -o hello.nasm
```

### 8.4 Test

```bash
hla64 test tests/samples
```

### 8.5 Bench

```bash
hla64 bench examples/checksum.hla64
```

### 8.6 Explain ABI

```bash
hla64 explain-abi --target linux-x64-sysv
```

---

## 9. Fase Eksekusi

## Fase 0 — Foundation & Repository Setup

### Tujuan

Menyiapkan fondasi repository, struktur solution, standar coding, dan dokumentasi awal.

### Deliverable

1. Repository `HlaX64`.
2. Solution `.NET`.
3. Project utama:
   - `HlaX64.Compiler`
   - `HlaX64.Backend.Nasm`
   - `HlaX64.Cli`
   - `HlaX64.Compiler.Tests`
4. README awal.
5. `docs/language-spec.md` awal.
6. `examples/hello.hla64` dummy.

### Kriteria selesai

1. `dotnet build` berhasil.
2. `dotnet test` berhasil.
3. CLI bisa dipanggil:

```bash
hla64 --version
```

### Catatan eksekusi untuk AI agent

Fokus hanya struktur. Jangan membuat compiler kompleks dulu.

---

## Fase 1 — Lexer & Parser MVP

### Tujuan

Membuat lexer dan parser untuk subset bahasa minimal.

### Syntax yang harus diparse

```hla
program hello;

begin hello;
    mov(1, rax);
end hello;
```

### Komponen

1. Tokenizer.
2. Parser recursive descent atau Pratt parser sederhana.
3. AST nodes:
   - ProgramNode
   - BlockNode
   - InstructionNode
   - ProcedureNode
   - CallNode
   - LiteralNode
   - RegisterNode
   - IdentifierNode
4. Diagnostics dengan line/column.

### Deliverable

1. Lexer tests.
2. Parser tests.
3. AST snapshot tests.
4. Error message yang jelas.

### Kriteria selesai

Parser bisa membaca:

```hla
program hello;
begin hello;
    mov(1, rax);
    add(2, rax);
end hello;
```

Dan menghasilkan AST tanpa crash.

---

## Fase 2 — NASM Backend MVP

### Tujuan

Menghasilkan NASM x64 dari AST sederhana.

### Input

```hla
program hello;
begin hello;
    mov(1, rax);
    add(2, rax);
end hello;
```

### Output NASM minimal

```asm
bits 64
global main

section .text
main:
    mov rax, 1
    add rax, 2
    ret
```

### Deliverable

1. `NasmEmitter`.
2. Snapshot test HLA → NASM.
3. CLI command:

```bash
hla64 emit-nasm examples/simple.hla64
```

### Kriteria selesai

1. Output NASM valid.
2. Snapshot test stabil.
3. Instruction operand order benar.

### Catatan penting

HLA-style biasanya memakai `mov(source, destination)`, sedangkan NASM memakai `mov destination, source`. Backend harus menangani perbedaan ini secara konsisten.

---

## Fase 3 — Toolchain Build Linux x64

### Tujuan

Membuat output yang benar-benar bisa dikompilasi dan dijalankan di Linux x64.

### Alur

```text
.hla64
  ↓
.nasm
  ↓ nasm -f elf64
.o
  ↓ gcc/ld
executable
```

### Deliverable

1. `hla64 build`.
2. `hla64 run`.
3. Toolchain detection untuk `nasm` dan `gcc`.
4. Error handling jika toolchain tidak ditemukan.

### Kriteria selesai

Program berikut bisa dijalankan:

```hla
program exitcode;
begin exitcode;
    mov(42, rax);
end exitcode;
```

Minimal program bisa build dan exit normal.

---

## Fase 4 — Runtime Minimal: stdout.put String

### Tujuan

Membuat `stdout.put` untuk string literal.

### Input

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

### Strategi awal

Untuk Linux, gunakan syscall `write` secara langsung atau libc `puts`. Untuk MVP, syscall lebih sederhana dan mengurangi dependency.

### Deliverable

1. String literal table.
2. NASM data section.
3. `stdout.put` lowering.
4. `nl` constant.

### Kriteria selesai

Program hello world mencetak teks ke terminal.

---

## Fase 5 — Semantic Analyzer

### Tujuan

Menambahkan validasi agar source tidak langsung dilempar ke backend tanpa pemeriksaan.

### Validasi awal

1. Nama program `begin/end` harus cocok.
2. Register valid.
3. Instruksi valid.
4. Jumlah operand valid.
5. Tipe literal valid.
6. Identifier harus dikenal.
7. Procedure tidak boleh duplikat.

### Deliverable

1. `SemanticAnalyzer`.
2. Diagnostic codes.
3. Unit tests untuk error.

### Contoh error yang bagus

```text
HLAX0012: Unknown register 'raxz' at line 4, column 12.
Did you mean 'rax'?
```

### Kriteria selesai

Source invalid menghasilkan error jelas, bukan exception mentah.

---

## Fase 6 — Procedure & Linux SysV ABI

### Tujuan

Mendukung procedure dan mapping argument sesuai Linux x64 System V ABI.

### Linux x64 argument registers

```text
1: rdi
2: rsi
3: rdx
4: rcx
5: r8
6: r9
return: rax
```

### Input

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;

program main;
begin main;
    call AddTwo(10, 20);
    stdout.put(rax, nl);
end main;
```

### Deliverable

1. Procedure symbol table.
2. Argument binding.
3. Function call lowering.
4. Return value convention.
5. ABI documentation.

### Kriteria selesai

Procedure dengan 1–6 argumen integer bisa dipanggil dan hasilnya benar.

---

## Fase 7 — Control Flow

### Tujuan

Mendukung `if/else/endif` dan `while/endwhile`.

### Input

```hla
if(rax = 0) then
    stdout.put("zero", nl);
else
    stdout.put("not zero", nl);
endif;
```

```hla
while(rcx < rdx) do
    add(1, rcx);
endwhile;
```

### Deliverable

1. AST untuk condition.
2. Label generator.
3. NASM compare/jump lowering.
4. Tests untuk nested control flow.

### Kriteria selesai

Loop dan branch sederhana berjalan benar.

---

## Fase 8 — Local Variables & Stack Frame

### Tujuan

Mendukung variabel lokal dasar dan stack frame sederhana.

### Syntax awal

```hla
procedure SumTo(n:int64); @returns("rax");
var
    total:int64;
    i:int64;
begin SumTo;
    mov(0, total);
    mov(0, i);

    while(i < n) do
        add(i, total);
        add(1, i);
    endwhile;

    mov(total, rax);
end SumTo;
```

### Deliverable

1. Local variable symbol table.
2. Stack offset allocation.
3. Prologue/epilogue generation.
4. Stack alignment handling.

### Kriteria selesai

Local variable bisa dipakai dalam arithmetic dan loop.

---

## Fase 9 — Test Runner

### Tujuan

Membuat test runner agar output program bisa diverifikasi otomatis.

### Format test manifest

```json
{
  "name": "hello",
  "source": "hello.hla64",
  "expected_stdout": "Hello from HlaX64\n",
  "expected_exit_code": 0
}
```

### CLI

```bash
hla64 test tests/samples
```

### Deliverable

1. Manifest parser.
2. Build-run-assert pipeline.
3. Output summary.

### Kriteria selesai

Minimal 10 sample tests bisa dijalankan otomatis.

---

## Fase 10 — Benchmark Runner

### Tujuan

Membandingkan performa routine HLA-X64 dengan baseline sederhana.

### CLI

```bash
hla64 bench examples/checksum.hla64
```

### Deliverable

1. Benchmark harness.
2. Timing loop.
3. Warmup.
4. Result summary.

### Kriteria selesai

Tool bisa menjalankan fungsi berkali-kali dan menampilkan waktu rata-rata.

---

## Fase 11 — Windows x64 Backend

### Tujuan

Menambahkan dukungan Windows x64 ABI.

### Windows x64 argument registers

```text
1: rcx
2: rdx
3: r8
4: r9
return: rax
shadow space: 32 bytes
stack alignment: 16 bytes
```

### Deliverable

1. Target `windows-x64-msabi`.
2. NASM Windows output atau MASM output.
3. Build via `nasm -f win64` + linker.
4. Console output Windows.
5. ABI tests.

### Kriteria selesai

Hello world dan procedure call bisa jalan di Windows x64.

---

## Fase 12 — C# Interop Generator

### Tujuan

Membuat native function dari HLA-X64 bisa dipanggil dari C#.

### Input

```hla
export procedure CountDelimiter(buffer:ptr; length:int64; delimiter:byte); @returns("rax");
begin CountDelimiter;
    // implementation
end CountDelimiter;
```

### Output C#

```csharp
internal static partial class NativeMethods
{
    [LibraryImport("hlax64lib")]
    internal static partial long CountDelimiter(nint buffer, long length, byte delimiter);
}
```

### Deliverable

1. Export metadata.
2. Native shared library build.
3. C# P/Invoke wrapper generator.
4. Example .NET console project.

### Kriteria selesai

C# bisa memanggil fungsi native hasil HLA-X64.

---

## Fase 13 — MCP Server untuk Vibe Coding Tools

### Tujuan

Membuat HLA-X64 bisa dipakai oleh AI coding agent sebagai tool.

### MCP tools awal

```text
compile_hla
emit_nasm
run_program
run_tests
benchmark_function
explain_abi
explain_error
```

### Use case

AI agent dapat menjalankan alur:

```text
Generate HLA-X64
  ↓
compile_hla
  ↓
run_tests
  ↓
fix if failed
  ↓
benchmark_function
```

### Deliverable

1. `HlaX64.McpServer`.
2. JSON schema untuk tools.
3. Dokumentasi integrasi Cursor/Codex/Claude-style agent.
4. Sample prompt.

### Kriteria selesai

MCP server bisa menerima source HLA-X64, compile, run, dan mengembalikan diagnostics.

---

## Fase 14 — AI Assembly Lab / IDE Plugin

### Tujuan

Membuat UI eksperimental untuk belajar dan menguji HLA-X64.

### Fitur awal

1. Editor source.
2. Tombol compile.
3. Panel NASM output.
4. Panel stdout/stderr.
5. Panel diagnostics.
6. Penjelasan ABI.
7. Prompt assistant.

### Stack yang disarankan

Untuk Anda, gunakan C#:

```text
Avalonia / WinUI / WPF
```

Jangan mulai fase ini sebelum CLI stabil.

### Kriteria selesai

User bisa menulis hello world, compile, run, dan melihat NASM output dari UI.

---

## 10. Prioritas Fitur

### P0 — Wajib

1. Parser.
2. NASM emitter.
3. CLI build/run.
4. Linux x64 hello world.
5. Procedure call.
6. Test runner.

### P1 — Penting

1. Control flow.
2. Local variables.
3. Semantic analyzer bagus.
4. Windows x64 backend.
5. C# interop generator.

### P2 — Menarik

1. MCP server.
2. Benchmark runner.
3. AI explanation.
4. VS Code extension.
5. GUI lab.

### P3 — Nanti

1. Macro system.
2. LLVM backend.
3. SIMD.
4. Optimizer.
5. OSDev target.
6. Full HLA compatibility layer.

---

## 11. Risiko dan Mitigasi

### Risiko 1 — Scope terlalu besar

Mitigasi:

- Jangan mengejar full HLA compatibility.
- Jangan membuat IDE dulu.
- Jangan membuat assembler object writer sendiri.
- Fokus pada subset kecil yang bisa jalan.

### Risiko 2 — ABI bug sulit ditemukan

Mitigasi:

- Dokumentasikan ABI.
- Buat test khusus calling convention.
- Buat runtime kecil dan eksplisit.
- Tambahkan `hla64 explain-abi`.

### Risiko 3 — AI menghasilkan source yang salah

Mitigasi:

- Diagnostics harus bagus.
- Test runner wajib.
- Benchmark bukan pengganti correctness test.
- Tambahkan kontrak function pada fase lanjut.

### Risiko 4 — Toolchain eksternal berbeda-beda

Mitigasi:

- Mulai dari Linux x64 via NASM.
- Deteksi versi NASM/GCC.
- Tampilkan instruksi install yang jelas.
- Windows ditunda sampai backend Linux stabil.

### Risiko 5 — Bahasa menjadi terlalu high-level

Mitigasi:

- Tetap dekat dengan register dan instruksi.
- Control flow boleh high-level, tetapi output harus jelas.
- Hindari fitur magic pada core language.

---

## 12. Definition of Done Per Milestone

### MVP selesai jika:

1. `hla64 build examples/hello.hla64` berhasil.
2. `hla64 run examples/hello.hla64` mencetak output benar.
3. Ada minimal 10 sample program.
4. Ada minimal 50 unit tests parser/backend/semantic.
5. Ada dokumentasi bahasa minimal.
6. Ada README dengan quick start.
7. Tidak ada crash mentah untuk input invalid umum.

### Alpha selesai jika:

1. Procedure call stabil.
2. Control flow stabil.
3. Local variables stabil.
4. Test runner stabil.
5. Linux x64 target usable.
6. Diagnostics cukup informatif.

### Beta selesai jika:

1. Windows x64 backend tersedia.
2. C# interop generator tersedia.
3. Benchmark runner tersedia.
4. MCP server awal tersedia.
5. Dokumentasi ABI lengkap.

---

## 13. Sample Program Target

### 13.1 Hello world

```hla
program hello;

#include("stdlib64.hhf")

begin hello;
    stdout.put("Hello from HlaX64", nl);
end hello;
```

### 13.2 Add two numbers

```hla
procedure AddTwo(a:int64; b:int64); @returns("rax");
begin AddTwo;
    mov(a, rax);
    add(b, rax);
end AddTwo;

program main;
begin main;
    call AddTwo(10, 20);
    stdout.put(rax, nl);
end main;
```

### 13.3 Count loop

```hla
program count;

#include("stdlib64.hhf")

begin count;
    mov(0, rcx);

    while(rcx < 10) do
        stdout.put(rcx, nl);
        add(1, rcx);
    endwhile;
end count;
```

### 13.4 Native routine candidate

```hla
export procedure CountDelimiter(buffer:ptr; length:int64; delimiter:byte); @returns("rax");
begin CountDelimiter;
    xor(rax, rax);
    xor(rcx, rcx);

    while(rcx < length) do
        // load byte, compare, increment count
        add(1, rcx);
    endwhile;
end CountDelimiter;
```

---

## 14. Prompt Eksekusi untuk GPT/Codex

Gunakan prompt ini untuk memulai fase 0:

```text
Anda adalah senior compiler engineer dan .NET engineer.

Saya ingin membuat proyek bernama HlaX64: HLA-inspired x64 assembly layer for executable vibe coding.

Tolong mulai dari Fase 0 saja.

Tujuan Fase 0:
1. Buat struktur repository .NET sesuai dokumen rencana.
2. Buat solution HlaX64.sln.
3. Buat project:
   - HlaX64.Compiler
   - HlaX64.Backend.Nasm
   - HlaX64.Cli
   - HlaX64.Compiler.Tests
4. Buat README awal.
5. Buat docs/language-spec.md awal.
6. Buat examples/hello.hla64 dummy.
7. Pastikan dotnet build dan dotnet test berhasil.

Batasan:
- Jangan membuat parser kompleks dulu.
- Jangan membuat backend kompleks dulu.
- Jangan membuat IDE.
- Jangan membuat Windows backend.
- Fokus hanya fondasi repository yang rapi dan siap dikembangkan.

Setelah selesai, jelaskan file apa saja yang dibuat dan cara menjalankan build/test.
```

Prompt untuk fase 1:

```text
Lanjutkan proyek HlaX64 ke Fase 1: Lexer & Parser MVP.

Tujuan:
- Implement tokenizer.
- Implement parser minimal untuk struktur:

program hello;
begin hello;
    mov(1, rax);
end hello;

- Buat AST nodes minimal.
- Buat diagnostics line/column.
- Tambahkan unit tests untuk lexer dan parser.

Batasan:
- Jangan membuat semantic analyzer penuh.
- Jangan membuat NASM backend dulu kecuali stub kecil diperlukan.
- Fokus pada parser yang bersih, mudah dites, dan error message yang jelas.
```

Prompt untuk fase 2:

```text
Lanjutkan proyek HlaX64 ke Fase 2: NASM Backend MVP.

Tujuan:
- Dari AST sederhana, hasilkan NASM x64.
- Dukung instruksi mov/add/sub/xor/cmp minimal.
- Ingat: syntax HLA menggunakan operand order source,destination; NASM menggunakan destination,source.
- Tambahkan snapshot tests HLA source → NASM output.
- Tambahkan CLI command emit-nasm.

Batasan:
- Jangan menambahkan runtime stdout dulu.
- Jangan procedure complex dulu.
- Fokus pada output NASM yang valid dan stabil.
```

---

## 15. Kesimpulan Strategis

Proyek ini sebaiknya tidak diposisikan sebagai:

```text
Pengganti NASM/MASM
```

Tetapi sebagai:

```text
AI-friendly x64 source layer for verified executable vibe coding
```

Nilai utamanya bukan hanya bahasa, tetapi pipeline:

```text
generate → compile → run → test → benchmark → explain
```

Dengan pendekatan ini, HlaX64 bisa menjadi proyek yang serius, unik, dan relevan dengan arah AI coding agent modern.

Mulailah kecil. Buat CLI yang bisa compile hello world dulu. Setelah itu, perluas secara disiplin.

