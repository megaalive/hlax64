# HlaX64 — Examples & How to Run

> **Status**: Aktif · selaras dengan [`HlaX64_Project_Plan.md`](../HlaX64_Project_Plan.md) §13.
> **Direktori**: [`examples/`](../examples) untuk program demo ·
> [`tests/samples/`](../tests/samples) untuk integration tests.

Dokumen ini adalah katalog singkat program contoh HlaX64 dan cara
menjalankannya. Untuk detail bahasa, lihat
[`docs/language-spec.md`](./language-spec.md) (akan di-rename ke
*Language Reference v0.1*).

---

## 1. Daftar contoh (`examples/`)

| File | Topik | Fase | Target |
|------|-------|------|--------|
| [`hello.hla64`](../examples/hello.hla64) | hello world + `stdout.put` | 4 | MVP |
| [`add_two.hla64`](../examples/add_two.hla64) | procedure 2-arg + call | 6 | MVP |
| [`count.hla64`](../examples/count.hla64) | while loop + register | 7 | MVP |
| [`simple.hla64`](../examples/simple.hla64) | mov + add | 2/3 | MVP |

---

## 2. Daftar sample tests (`tests/samples/`)

Sample di folder ini punya `manifest.json` dan dijalankan lewat
`hla64 test tests/samples`. Saat ini tersedia **16 sample**, semuanya PASS.

| Sample | Topik | Status |
|--------|-------|--------|
| `hello/` | hello + `stdout.put` | ✅ |
| `exitcode/` | exit code via `rax` | ✅ |
| `add_two/` | procedure call | ✅ |
| `count/` | while loop | ✅ |
| `simple/` | mov + add | ✅ |
| `local_var/` | `var` block | ✅ |
| `if_else/` | `if/else/endif` | ✅ |
| `procedure_0arg/` | procedure tanpa argumen | ✅ |
| `procedure_1arg/` | procedure 1 argumen | ✅ |
| `procedure_6args/` | procedure 6 argumen (max SysV) | ✅ |
| `comparison_signed/` | signed `jg` / `jl` | ✅ |
| `comparison_unsigned/` | unsigned `ja` / `jb` | ✅ |
| `stdout_int64/` | stdout.put dengan register int64 | ✅ |
| `callee_saved/` | callee-saved register preservation | ✅ |
| `stack_alignment/` | cek RSP mod 16 | ✅ |
| `export_lib/` | shared library export + C# P/Invoke | ✅ |

---

## 3. Cara menjalankan

### 3.1 Prasyarat

- `dotnet` 10.0+ di build machine.
- `nasm` dan `gcc` di runtime machine (atau WSL2 / MinGW).

Build toolchain:

```bash
dotnet build
```

### 3.2 Compile & run satu program

```bash
# Emit NASM saja
dotnet run --project src/HlaX64.Cli -- emit-nasm examples/hello.hla64

# Build menjadi executable
dotnet run --project src/HlaX64.Cli -- build examples/hello.hla64 -o build/hello

# Compile + run
dotnet run --project src/HlaX64.Cli -- run examples/hello.hla64
```

Output biner ada di `build/<name>/<name>` (atau `-o`).

### 3.3 Menjalankan test runner

```bash
# Semua sample di folder
dotnet run --project src/HlaX64.Cli -- test tests/samples

# Filter berdasarkan nama
dotnet run --project src/HlaX64.Cli -- test tests/samples --filter hello

# Output JSON (untuk agent)
dotnet run --project src/HlaX64.Cli -- test tests/samples --json
```

### 3.4 Format manifest

Setiap sample di `tests/samples/<name>/manifest.json`:

```json
{
  "name": "hello",
  "source": "hello.hla64",
  "expected_stdout": "Hello from HlaX64\n",
  "expected_exit_code": 0
}
```

Field:

- `name` — nama sample (untuk pelaporan).
- `source` — file `.hla64` (path relatif terhadap folder sample).
- `expected_stdout` — stdout persis yang diharapkan.
- `expected_exit_code` — exit code integer.

### 3.5 Aturan Linux CI (planned, Fase 9.5)

```bash
dotnet restore
dotnet build --no-restore
dotnet test --no-build

# Build & run sample binaries
for s in tests/samples/*/; do
  dotnet run --project src/HlaX64.Cli -- build "$s*.hla64"
done
```

CI gagal jika ada binary yang crash, stdout berbeda, atau exit code
berbeda dari manifest.

---

## 4. Konvensi penamaan

- File program: `snake_case.hla64` (lowercase + underscore).
- Folder sample test: `snake_case/` dengan manifest dan source di
  dalamnya.
- Procedure: `PascalCase` (`AddTwo`, `CountDelimiter`).
- Local variable: `snake_case` (`total`, `byte_count`).
- Register: lowercase sesuai arsitektur (`rax`, `rdi`, `r10d`).

---

## 5. Lihat juga

- [`HlaX64_Project_Plan.md` §13 Sample Program Target](../HlaX64_Project_Plan.md) — deskripsi program contoh.
- [`HlaX64_Project_Plan.md` §13.5 Native integration test samples](../HlaX64_Project_Plan.md) — daftar untuk Fase 9.5.
- [`docs/abi-linux-x64.md`](./abi-linux-x64.md) — detail ABI Linux SysV.
- [`docs/runtime-contract.md`](./runtime-contract.md) — kontrak runtime.
