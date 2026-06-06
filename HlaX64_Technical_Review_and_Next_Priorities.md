# HlaX64 — Review Teknis, Temuan, dan Prioritas Pengerjaan Berikutnya

**Repository:** `megaalive/hlax64`  
**Dokumen:** Review teknis dan rencana stabilisasi arsitektur  
**Status:** Draft kerja untuk eksekusi dengan GPT-5.5/Codex atau model yang lebih baru

---

## 1. Ringkasan Eksekutif

Proyek HlaX64 sudah melewati tahap eksperimen awal. Fondasi utama seperti lexer, parser, AST, semantic analysis, backend NASM, CLI, runtime awal, test runner, dokumentasi, dan unit test telah menunjukkan bahwa konsep ini dapat diwujudkan sebagai toolchain nyata.

Risiko terbesar proyek saat ini bukan lagi kegagalan membuat fitur, melainkan pertumbuhan fitur terlalu cepat sebelum fondasi berikut stabil:

- model target dan output;
- intermediate representation;
- lowering ABI;
- kontrak register;
- aturan tipe dan ukuran data;
- runtime;
- native integration test;
- konsistensi dokumentasi.

Karena itu, rekomendasi utama adalah menghentikan sementara penambahan syntax baru dan menyisipkan satu fase stabilisasi arsitektur sebelum melanjutkan benchmark runner, backend Windows, interop C#, MCP server, atau IDE.

Prioritas tertinggi:

```text
AST
  ↓
Semantic Model
  ↓
Lowered IR
  ↓
ABI Lowering
  ↓
Backend NASM
  ↓
Assembler + Linker
  ↓
Native Integration Test
```

---

# 2. Hal yang Sudah Kuat

## 2.1 Positioning proyek

Positioning proyek sudah tepat:

```text
AI-friendly x64 assembly source layer for verified executable vibe coding
```

HlaX64 tidak perlu diposisikan sebagai pengganti NASM, MASM, atau LLVM. Nilai utamanya adalah menjadi lapisan low-level yang:

- lebih mudah ditulis AI;
- lebih mudah dibaca manusia;
- tetap dekat dengan CPU;
- dapat diturunkan menjadi assembly nyata;
- dapat diuji secara deterministik;
- dapat dijelaskan kembali oleh agent.

## 2.2 Vertical slice sudah nyata

Pipeline dasar sudah menghasilkan jalur vertikal:

```text
.hla64
→ lexer
→ parser
→ semantic analysis
→ NASM
→ object file
→ executable
→ run/test
```

Ini lebih bernilai daripada menambahkan banyak fitur bahasa yang belum menghasilkan program executable.

## 2.3 Struktur solution sudah sehat

Pemisahan proyek seperti compiler, backend, CLI, dan runtime merupakan keputusan baik. Struktur ini memungkinkan penambahan:

- backend Windows;
- ABI layer;
- MCP server;
- LSP;
- interop generator;
- benchmark runner;
- debugger integration;

tanpa mengotori compiler core.

## 2.4 Identitas bahasa tetap jelas

Penggunaan ekstensi `.hla64` dan operand order bergaya HLA:

```hla
mov(source, destination);
```

merupakan keputusan baik. Ini menjaga identitas proyek tanpa mengklaim kompatibilitas penuh dengan HLA klasik.

## 2.5 Testing sudah menjadi bagian desain

Keberadaan test lexer, parser, semantic analysis, emitter, dan test runner menunjukkan bahwa proyek dikerjakan sebagai toolchain serius, bukan sekadar demo.

---

# 3. Temuan yang Perlu Dibenahi

## 3.1 Dokumentasi fase dan versi bahasa belum sinkron

### Masalah

Dokumentasi proyek masih mencampur dua hal:

- fase pengerjaan;
- versi atau kematangan bahasa.

Contoh masalah umum:

```text
Fase 0–4 selesai
```

padahal implementasi sudah memiliki fitur setelah fase tersebut.

### Risiko

- pengguna tidak tahu kemampuan aktual;
- AI agent membaca informasi yang saling bertentangan;
- roadmap sulit dipercaya;
- dokumentasi cepat basi setiap kali fase bergeser.

### Rekomendasi

Pisahkan status menjadi:

```text
Project phase: Phase 9 completed
Language version: 0.1
Specification status: Draft
Implemented target: linux-x64-sysv
```

Jangan memakai nomor fase sebagai identitas spesifikasi bahasa.

### Acceptance criteria

- README, roadmap, dan language specification memiliki status yang sama;
- versi bahasa ditulis eksplisit;
- target yang sudah didukung ditulis eksplisit;
- fitur eksperimental diberi label;
- fitur yang belum didukung tidak ditampilkan seolah-olah tersedia.

---

## 3.2 Dokumentasi Test Runner CLI harus eksplisit

### Masalah

Test runner telah disebut sebagai fitur, tetapi command aktual, format manifest, exit code, dan output belum tentu mudah ditemukan oleh pengguna.

### Rekomendasi

Tambahkan bagian khusus:

```bash
hla64 test
hla64 test tests/manifest.json
hla64 test --json
hla64 test --filter arithmetic
```

Dokumentasikan:

- lokasi default manifest;
- schema manifest;
- arti exit code;
- format stdout/stderr;
- mode JSON untuk agent;
- perilaku saat build gagal;
- perilaku saat test timeout.

### Acceptance criteria

Pengguna baru dapat:

1. clone repo;
2. build;
3. menjalankan satu contoh;
4. menjalankan test manifest;

tanpa membaca source code.

---

## 3.3 Compiler membutuhkan Intermediate Representation

### Masalah

Alur langsung:

```text
AST → NASM emitter
```

cukup untuk MVP, tetapi akan menjadi beban saat menambahkan:

- Windows x64 ABI;
- output DLL/shared library;
- backend MASM;
- LLVM;
- optimisasi;
- register validation;
- stack spilling;
- debug information.

### Risiko

Emitter akan dipenuhi kondisi seperti:

```csharp
if (target == Windows) ...
else if (target == Linux) ...
```

Akibatnya:

- backend menjadi sulit dirawat;
- semantic logic bocor ke emitter;
- testing menjadi rapuh;
- multi-target menjadi mahal.

### Rekomendasi

Tambahkan lowered IR yang netral terhadap syntax source dan sebisa mungkin netral terhadap ABI.

Contoh:

```text
Function AddTwo
Parameters:
  a: i64
  b: i64

Block entry:
  v0 = LoadParameter a
  v1 = LoadParameter b
  v2 = Add v0, v1
  Return v2
```

Setelah itu:

```text
IR
→ SysV ABI lowerer
→ NASM emitter
```

atau:

```text
IR
→ Microsoft x64 ABI lowerer
→ NASM/MASM emitter
```

### Struktur yang disarankan

```text
HlaX64.Compiler
├─ Syntax
├─ Parsing
├─ Binding
├─ Symbols
├─ Types
├─ Diagnostics
└─ Compilation

HlaX64.CodeGen
├─ Ir
├─ Lowering
├─ ControlFlow
└─ Abi
   ├─ SysV
   └─ MicrosoftX64

HlaX64.Backend.Nasm
└─ NasmEmitter
```

### Acceptance criteria

- AST tidak lagi menulis NASM secara langsung;
- semua procedure diturunkan ke IR;
- unit test IR tersedia;
- backend hanya menerima IR/lowered instructions;
- penambahan ABI baru tidak membutuhkan perubahan parser.

---

## 3.4 Register ownership dan clobber contract harus formal

### Masalah

Bahasa memperbolehkan user memakai register secara langsung, tetapi compiler dan runtime juga membutuhkan register untuk:

- output;
- konversi integer;
- syscall;
- procedure call;
- comparison;
- local variables;
- temporary values.

Tanpa aturan formal, compiler dapat menimpa nilai user secara diam-diam.

### Rekomendasi

Definisikan:

```text
User-visible registers
Compiler-reserved registers
Caller-saved registers
Callee-saved registers
Runtime-clobbered registers
Scratch registers
Return registers
Argument registers
```

Untuk Linux SysV, minimal dokumentasikan:

```text
Caller-saved:
rax rcx rdx rsi rdi r8 r9 r10 r11

Callee-saved:
rbx rbp r12 r13 r14 r15
```

Setiap runtime function perlu metadata, misalnya:

```text
hla64_stdout_put_i64
Inputs:
  rdi = value

Clobbers:
  rax rcx rdx rsi rdi r8 r9 r10 r11
```

### Tahap implementasi

1. dokumentasikan clobber;
2. simpan clobber metadata di compiler;
3. keluarkan warning untuk register conflict yang jelas;
4. kelak tambahkan liveness analysis sederhana.

### Acceptance criteria

- tidak ada helper runtime tanpa clobber contract;
- compiler memiliki daftar register reserved;
- procedure prologue/epilogue menjaga callee-saved register;
- integration test memverifikasi register preservation.

---

## 3.5 Type-size correctness harus diperketat

### Masalah

Tipe seperti:

```text
int8
int16
int32
int64
uint8
uint16
uint32
uint64
```

tidak boleh hanya dianggap sebagai alias qword.

### Risiko

- memory overwrite;
- hasil sign extension salah;
- truncation tidak disengaja;
- comparison salah;
- output integer salah;
- bug hanya muncul pada nilai boundary.

### Rekomendasi

Compiler harus membedakan ukuran operand:

```nasm
mov byte  [rbp-1], al
mov word  [rbp-4], ax
mov dword [rbp-8], eax
mov qword [rbp-16], rax
```

Tambahkan aturan eksplisit untuk:

- sign extension;
- zero extension;
- narrowing conversion;
- widening conversion;
- immediate overflow;
- storage alignment;
- arithmetic promotion;
- return value width.

Contoh diagnostic:

```text
HLA1204: Cannot implicitly store int64 into int8.
Use trunc8(...) for an explicit narrowing conversion.
```

### Acceptance criteria

- setiap tipe memiliki size dan signedness;
- semantic analyzer menolak implicit narrowing;
- boundary test tersedia untuk min/max setiap tipe;
- emitter menghasilkan operand size yang benar;
- comparison mengikuti signedness.

---

## 3.6 Signed dan unsigned comparison harus dibedakan

### Masalah

Instruksi jump berbeda untuk signed dan unsigned:

```text
Signed:
jl jle jg jge

Unsigned:
jb jbe ja jae
```

Menggunakan signed comparison untuk `uint64` akan menghasilkan bug pada nilai dengan high bit aktif.

### Rekomendasi

Comparison IR harus membawa informasi:

```text
CompareKind:
- Equal
- NotEqual
- LessThanSigned
- LessThanUnsigned
- LessOrEqualSigned
- LessOrEqualUnsigned
- GreaterThanSigned
- GreaterThanUnsigned
- GreaterOrEqualSigned
- GreaterOrEqualUnsigned
```

### Acceptance criteria

Test wajib mencakup:

```text
0
1
-1
int64.MinValue
int64.MaxValue
uint64.MaxValue
0x8000000000000000
```

---

## 3.7 Procedure call contract perlu difinalkan

### Area yang harus ditetapkan

- evaluation order argumen;
- maksimum argumen;
- argumen di stack;
- nested calls;
- recursion;
- register spilling;
- stack alignment;
- callee-saved preservation;
- return type;
- variadic call;
- external call;
- tail call;
- function pointer.

### Rekomendasi MVP

Untuk versi 0.1:

```text
- maksimal 6 integer/pointer arguments;
- evaluation order: left-to-right;
- return value: rax;
- tidak mendukung variadic;
- tidak mendukung nested expression call;
- recursion boleh hanya jika stack frame telah teruji;
- unsupported form harus ditolak semantic analyzer.
```

Jangan membiarkan backend menghasilkan kode yang “mungkin benar”.

### Acceptance criteria

- calling convention didokumentasikan;
- call dengan 0–6 argumen memiliki integration test;
- nested unsupported call memberi diagnostic;
- stack alignment diperiksa sebelum setiap call;
- callee-saved register diuji;
- recursive factorial atau fibonacci memiliki test bila recursion didukung.

---

## 3.8 Output kind dan entry point harus dipisahkan

### Masalah

Konsep `program` dan `_start` cocok untuk standalone Linux executable, tetapi tidak cocok untuk:

- object file;
- static library;
- shared library;
- Windows DLL;
- C ABI export;
- C# P/Invoke.

### Rekomendasi

Tambahkan:

```csharp
enum OutputKind
{
    Executable,
    ObjectFile,
    StaticLibrary,
    SharedLibrary,
    AssemblyOnly
}
```

Tambahkan target model:

```text
TargetTriple:
- x86_64-linux-gnu
- x86_64-linux-none
- x86_64-windows-msvc
- x86_64-windows-gnu
```

Atau versi MVP yang lebih sederhana:

```text
linux-x64-sysv
windows-x64-msabi
```

### Acceptance criteria

- `_start` hanya dibuat untuk executable standalone;
- library target tidak menghasilkan entry point;
- export symbol dapat ditentukan;
- CLI menerima `--output-kind`;
- tests tersedia untuk assembly-only dan executable.

---

## 3.9 Strategi runtime perlu diputuskan

### Masalah

Inline syscall dan runtime library yang hidup bersamaan terlalu lama akan membuat perilaku terpecah.

### Rekomendasi

Gunakan runtime library sebagai default:

```text
source
→ call hla64_stdout_put_i64
→ link HlaX64.Runtime
```

Inline syscall dapat menjadi mode khusus:

```bash
hla64 build --runtime inline
```

### Keuntungan runtime library

- backend lebih sederhana;
- perilaku output konsisten;
- dapat diuji terpisah;
- mudah di-port ke Windows;
- mudah menambahkan string, memory, file, math;
- ABI helper dapat didokumentasikan.

### Acceptance criteria

- default build memakai runtime object/library;
- inline mode bersifat opsional;
- runtime mempunyai semantic version;
- runtime export memiliki ABI contract;
- runtime integration test berjalan pada CI.

---

## 3.10 CI harus membuktikan binary benar-benar berjalan

### Masalah

Unit test lexer/parser/emitter belum membuktikan bahwa output assembly valid dan executable bekerja.

### Rekomendasi CI Linux

```text
dotnet restore
dotnet build --no-restore
dotnet test --no-build

install nasm
build examples
run examples
validate stdout
validate stderr
validate exit code
run manifest tests
```

### Rekomendasi CI Windows saat backend tersedia

```text
dotnet build
dotnet test
install/use assembler and linker
build Windows examples
run binaries
validate output
```

### Acceptance criteria

CI gagal bila:

- NASM gagal;
- linker gagal;
- executable crash;
- stdout berbeda;
- exit code berbeda;
- stack alignment test gagal;
- runtime test gagal.

---

## 3.11 Klaim dokumentasi harus sesuai kematangan

### Masalah

Istilah seperti:

```text
Full language reference
```

terlalu kuat untuk specification draft yang masih berkembang.

### Rekomendasi

Gunakan:

```text
Draft Language Specification
Current Language Reference
Language Reference v0.1
```

Dokumen specification sebaiknya mencakup:

- lexical grammar;
- syntax grammar;
- type system;
- conversion rules;
- expression evaluation;
- procedure semantics;
- register semantics;
- runtime model;
- target ABI;
- diagnostics;
- undefined behavior;
- implementation limits;
- versioning policy.

---

# 4. Fase Prioritas Berikutnya

## Fase 9.5 — Compiler Architecture Stabilization

Fase ini sebaiknya disisipkan sebelum benchmark runner atau backend Windows.

### Tujuan

Menjadikan compiler core cukup stabil untuk:

- multi-ABI;
- multi-output;
- runtime library;
- interop;
- MCP;
- native test;
- penambahan fitur bahasa tanpa merusak backend.

### Deliverable utama

1. `TargetTriple`
2. `OutputKind`
3. `CompilationOptions`
4. semantic type model
5. lowered IR
6. SysV ABI lowerer
7. NASM emitter dari IR
8. register/clobber contract
9. native integration test
10. sinkronisasi dokumentasi

---

## 4.1 Workstream A — Compilation model

Buat model pusat:

```csharp
public sealed record CompilationOptions(
    TargetTriple Target,
    OutputKind OutputKind,
    RuntimeMode RuntimeMode,
    OptimizationLevel Optimization,
    bool EmitDebugInfo);
```

### Acceptance criteria

- target tidak lagi berupa string tersebar;
- semua backend memakai objek konfigurasi yang sama;
- unsupported combination memberi diagnostic;
- CLI memetakan argumen ke `CompilationOptions`.

---

## 4.2 Workstream B — Semantic type system

Buat representasi tipe eksplisit:

```csharp
public sealed record IntegerTypeSymbol(
    string Name,
    int BitWidth,
    bool IsSigned);
```

Tambahkan:

- implicit conversion rules;
- explicit conversion;
- operand compatibility;
- return type validation;
- literal range validation.

### Acceptance criteria

- compiler mengetahui signedness dan width;
- semua operasi binary divalidasi;
- overflow literal dideteksi;
- test boundary tersedia.

---

## 4.3 Workstream C — IR

Minimal IR v0.1:

```text
IrFunction
IrBasicBlock
IrValue
IrInstruction

Instructions:
- LoadConstant
- LoadLocal
- StoreLocal
- Move
- Add
- Subtract
- Multiply
- Divide
- Compare
- Branch
- ConditionalBranch
- Call
- Return
```

### Acceptance criteria

- procedure sederhana dapat diturunkan ke IR;
- if/else dan while menjadi basic block;
- IR bisa dicetak untuk debugging;
- snapshot test tersedia.

---

## 4.4 Workstream D — ABI lowering

Buat interface:

```csharp
public interface IAbiLowerer
{
    LoweredFunction Lower(IrFunction function, CompilationOptions options);
}
```

Implementasi awal:

```text
SysVAbiLowerer
```

Tanggung jawab:

- assign argument registers;
- define return register;
- preserve callee-saved registers;
- calculate stack frame;
- enforce stack alignment;
- lower calls;
- define scratch registers.

### Acceptance criteria

- ABI logic tidak berada di parser;
- ABI logic tidak tersebar di emitter;
- function call test 0–6 argumen lolos;
- stack alignment diuji.

---

## 4.5 Workstream E — Backend NASM

Backend NASM hanya bertugas:

- section declaration;
- symbol emission;
- instruction formatting;
- label formatting;
- data formatting;
- target syntax details.

Backend tidak boleh memutuskan semantic type atau calling convention.

### Acceptance criteria

- emitter menerima lowered representation;
- emitter tidak membaca AST;
- emitter tidak memutuskan signedness;
- emitter tidak menghitung ABI argument placement.

---

## 4.6 Workstream F — Native integration tests

Buat fixture yang:

1. menulis source sementara;
2. menjalankan compiler;
3. menjalankan NASM;
4. menjalankan linker;
5. menjalankan binary;
6. memeriksa output dan exit code.

### Test minimum

```text
hello string
stdout int64
arithmetic
signed comparison
unsigned comparison
if/else
while
procedure 0 arg
procedure 1 arg
procedure 6 args
local variables
callee-saved preservation
stack alignment
runtime linking
invalid source diagnostics
```

### Acceptance criteria

Semua test berjalan otomatis di CI Linux.

---

## 4.7 Workstream G — Dokumentasi

Perbarui:

```text
README.md
docs/language-spec.md
docs/abi-linux-sysv.md
docs/compiler-architecture.md
docs/runtime-contract.md
docs/roadmap.md
```

Tambahkan diagram:

```text
Source
→ Syntax Tree
→ Bound Tree
→ IR
→ ABI Lowering
→ NASM
→ Object
→ Executable
```

---

# 5. Urutan Roadmap yang Direkomendasikan

```text
Phase 9.5  Architecture stabilization
Phase 10   Benchmark runner
Phase 11   Windows x64 backend
Phase 12   C ABI export and C# P/Invoke generator
Phase 13   MCP server
Phase 14   LSP and editor tooling
Phase 15   AI Assembly Lab / GUI
```

---

# 6. Fase 10 — Benchmark Runner

Kerjakan setelah IR dan native integration test stabil.

## Tujuan

Memungkinkan agent membandingkan:

- baseline C#;
- HlaX64;
- NASM output;
- beberapa implementasi HlaX64;
- perubahan optimisasi.

## Command target

```bash
hla64 bench
hla64 bench benchmarks/sum-bytes.json
hla64 bench --json
```

## Data yang dikumpulkan

- warmup count;
- iteration count;
- mean;
- median;
- min/max;
- standard deviation;
- binary size;
- compile duration;
- test pass/fail.

## Catatan penting

Benchmark bukan alat pembuktian correctness. Test harus selalu dijalankan sebelum benchmark.

---

# 7. Fase 11 — Windows x64 Backend

Kerjakan setelah ABI dipisahkan dari emitter.

## Fokus utama

- RCX, RDX, R8, R9 argument registers;
- 32-byte shadow space;
- 16-byte stack alignment;
- nonvolatile register preservation;
- PE/COFF toolchain;
- executable dan DLL output;
- Windows runtime.

## Acceptance criteria

- hello executable;
- procedure 0–4 args;
- procedure dengan stack args;
- C ABI export;
- DLL dipanggil dari C#;
- CI Windows hijau.

---

# 8. Fase 12 — C ABI dan C# Interop

## Tujuan

Membuat routine HlaX64 dapat digunakan aplikasi modern.

Contoh:

```hla
export procedure SumBytes(
    buffer: ptr uint8;
    length: uint64
): uint64;
```

Generate:

```csharp
[LibraryImport("sample")]
internal static partial ulong SumBytes(
    nint buffer,
    ulong length);
```

## Deliverable

- export syntax;
- C header generator;
- C# P/Invoke generator;
- native library build;
- sample console app;
- integration test.

---

# 9. Fase 13 — MCP Server

## Tujuan

Mengintegrasikan HlaX64 dengan Codex, Claude Code, Cursor, atau agent lain.

## Tool minimum

```text
compile
emit_nasm
run
test
benchmark
inspect_abi
explain_diagnostic
get_ir
get_disassembly
```

## Prinsip

- semua tool mendukung JSON;
- output deterministik;
- timeout;
- sandbox;
- path restriction;
- tidak menjalankan binary tanpa izin eksplisit dari host.

---

# 10. Hal yang Sebaiknya Ditunda

Jangan dikerjakan sebelum fase stabilisasi selesai:

- GUI besar;
- custom object-file writer;
- optimizer kompleks;
- full macro language;
- LLVM backend;
- debugger buatan sendiri;
- OS kernel;
- package manager;
- kompatibilitas penuh HLA klasik;
- register allocator canggih;
- JIT compiler;
- self-hosting compiler.

Semua hal tersebut menarik, tetapi dapat mengalihkan proyek dari tujuan utama.

---

# 11. Definition of Done untuk Stabilisasi

Fase 9.5 dianggap selesai bila:

- [ ] AST tidak lagi langsung menghasilkan NASM.
- [ ] IR tersedia dan memiliki snapshot test.
- [ ] SysV ABI lowerer terpisah.
- [ ] NASM emitter hanya memformat lowered instructions.
- [ ] `TargetTriple` tersedia.
- [ ] `OutputKind` tersedia.
- [ ] tipe memiliki bit width dan signedness.
- [ ] signed dan unsigned comparison benar.
- [ ] narrowing conversion ditolak tanpa cast eksplisit.
- [ ] register clobber contract terdokumentasi.
- [ ] callee-saved register dipertahankan.
- [ ] stack alignment diuji.
- [ ] runtime library menjadi jalur default.
- [ ] native integration test berjalan di Linux CI.
- [ ] README, spec, ABI docs, dan roadmap sinkron.

---

# 12. Prompt Eksekusi untuk GPT-5.5/Codex

Salin prompt berikut saat akan mengeksekusi fase stabilisasi:

```text
Anda bekerja pada repository HlaX64, sebuah HLA-inspired x64 compiler
berbasis C# dengan backend NASM Linux x64.

Tujuan pekerjaan ini adalah menyelesaikan Phase 9.5:
Compiler Architecture Stabilization.

Jangan menambahkan fitur syntax baru kecuali benar-benar diperlukan untuk
refactor. Fokus pada arsitektur, correctness, testing, dan dokumentasi.

Target akhir:

1. Tambahkan TargetTriple, OutputKind, RuntimeMode, dan CompilationOptions.
2. Pisahkan AST/bound model dari code generation.
3. Buat lowered IR minimal untuk constant, local, arithmetic, comparison,
   branch, loop, call, dan return.
4. Buat SysV ABI lowerer terpisah.
5. Ubah NASM emitter agar menerima lowered representation, bukan AST.
6. Formalisasikan register ownership dan clobber contract.
7. Perbaiki type width, signedness, implicit conversion, dan comparison.
8. Tambahkan native integration tests menggunakan NASM dan linker.
9. Pastikan semua unit test lama tetap lolos.
10. Sinkronkan README, language spec, ABI docs, architecture docs, dan roadmap.

Aturan pengerjaan:

- Kerjakan secara incremental.
- Setelah setiap perubahan besar, jalankan dotnet build dan dotnet test.
- Jangan menghapus test yang gagal hanya agar pipeline hijau.
- Pertahankan kompatibilitas syntax existing sejauh masuk akal.
- Bila perubahan breaking diperlukan, dokumentasikan secara eksplisit.
- Jangan menaruh logic ABI di parser atau NASM formatter.
- Jangan membuat optimizer kompleks.
- Jangan membuat backend Windows pada fase ini.
- Gunakan diagnostic yang jelas untuk fitur unsupported.
- Tambahkan test untuk setiap bug atau edge case yang ditemukan.

Urutan implementasi:

A. Audit struktur saat ini.
B. Buat compilation model.
C. Buat semantic type model.
D. Buat IR.
E. Implementasikan lowering AST/bound tree ke IR.
F. Implementasikan SysV ABI lowering.
G. Refactor NASM emitter.
H. Tambahkan runtime contract.
I. Tambahkan native integration test.
J. Perbarui dokumentasi.
K. Buat laporan akhir berupa:
   - file yang berubah;
   - keputusan desain;
   - test yang ditambahkan;
   - keterbatasan tersisa;
   - rekomendasi fase selanjutnya.

Sebelum mengubah kode, baca seluruh project structure dan test yang ada.
Jangan berasumsi bahwa README sepenuhnya sinkron dengan implementasi.
Gunakan source code dan test sebagai sumber kebenaran utama.
```

---

# 13. Kesimpulan

HlaX64 telah membuktikan bahwa konsepnya layak. Fokus berikutnya bukan memperbanyak fitur, tetapi membuat fondasi compiler cukup kuat agar proyek dapat berkembang menjadi:

- compiler multi-platform;
- runtime native;
- library generator;
- MCP tool untuk coding agent;
- backend executable vibe coding;
- platform edukasi low-level.

Keputusan paling penting saat ini:

```text
Stop adding syntax temporarily.
Stabilize IR, ABI, types, runtime, and native tests first.
```

Langkah tersebut akan membedakan HlaX64 dari transpiler eksperimental menjadi toolchain yang dapat dikembangkan secara serius.
