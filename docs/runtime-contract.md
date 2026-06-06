# HlaX64 — Runtime Contract

> **Status**: Final · Fase 9.5 (Workstream H) completed  
> **Lokasi runtime**: [`src/HlaX64.Runtime/`](../src/HlaX64.Runtime)  
> **Lihat juga**: [`docs/compiler-architecture.md`](./compiler-architecture.md) ·  
> [`HlaX64_Project_Plan.md` §9.5 (Workstream H)](../HlaX64_Project_Plan.md)

Dokumen ini adalah **kontrak ABI** antara compiler HlaX64 dan runtime
library-nya. Setiap fungsi runtime yang dipanggil dari kode terkompilasi
**wajib** mendeklarasikan metadata clobber, dan compiler **wajib**
menghormati kontrak tersebut (terutama pada save/restore callee-saved
registers dan stack alignment).

> **Latar belakang**: Temuan 3.4 (Register ownership & clobber contract
> harus formal) dan Temuan 3.9 (runtime library sebagai jalur default,
> `--runtime inline` bersifat opsi).

---

## 1. Daftar fungsi runtime MVP

| Symbol | Modul | Tujuan | Status |
|--------|-------|--------|--------|
| `hla64_stdout_put_str` | `stdout.nasm` | cetak string + newline opsional | MVP |
| `hla64_stdout_put_i64` | `conversion.nasm` + `stdout.nasm` | cetak int64 desimal | MVP |
| `hla64_stdout_put_u64` | `conversion.nasm` + `stdout.nasm` | cetak uint64 desimal | planned |
| `hla64_i64_to_str`     | `conversion.nasm`            | integer → ascii buffer | planned |
| `hla64_str_len`        | `stdout.nasm`                | panjang string (nol-terminated) | planned |
| `hla64_memcpy`         | `mem.nasm`                   | salin byte | planned (Fase 9.5) |
| `hla64_memset`         | `mem.nasm`                   | isi byte | planned (Fase 9.5) |
| `hla64_exit`           | `startup.nasm`               | syscall `exit(rdi)` | MVP |

Tambahan akan muncul seiring Fase 9.5 → 10 → 11. Setiap entry baru
harus terdaftar di tabel ini dan di `src/HlaX64.Runtime/abi-contract.md`.

---

## 2. Format metadata clobber

Setiap fungsi runtime menyertakan **header komentar** dengan format
tetap yang bisa di-parse compiler.

### 2.1 Contoh header

```nasm
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    hla64_stdout_put_str
; target:  linux-x64-sysv
; inputs:
;   rdi = pointer ke null-terminated string
;   rsi = boolean (0/1) cetak newline di akhir
; clobbers:
;   rax, rcx, rdx, rsi, rdi, r8, r9, r10, r11
; preserves:
;   rbx, rbp, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 pada saat call
; notes:
;   Memanggil syscall write; mensyaratkan fd=1 (stdout) sudah aktif.
```

### 2.2 Field wajib

- `HLAX64-RUNTIME-FUNCTION v0.1` — penanda kontrak.
- `name:` — symbol global yang bisa di-`extern`.
- `target:` — target ABI (`linux-x64-sysv`, `windows-x64-msabi`).
- `inputs:` — argumen menurut konvensi ABI target.
- `clobbers:` — register yang **tidak** dijaga oleh fungsi.
- `preserves:` — register yang **dijaga** (callee-saved).
- `stack-align:` — prasyarat alignment stack dari caller.
- `notes:` — perilaku tambahan, efek samping, prasyarat environment.

### 2.3 Parser metadata (planned)

Tool `hla64 inspect-runtime` akan membaca header ini dari file `.nasm`
dan menghasilkan `RuntimeFunctionDescriptor` (C# record) yang dipakai
oleh compiler untuk:

- emit `extern` symbol,
- emit prologue/epilogue yang menjaga callee-saved,
- emit stack alignment check sebelum call (debug mode),
- emit warning bila ada register conflict dengan source user.

---

## 3. Kontrak SysV (Linux x64) v0.1

```text
Caller-saved  : rax, rcx, rdx, rsi, rdi, r8, r9, r10, r11
Callee-saved  : rbx, rbp, r12, r13, r14, r15
Scratch       : rax, rcx, rdx, rsi, rdi, r8, r9, r10, r11
Return        : rax (64-bit), rax+rdx (struct kecil)
Args (1..6)   : rdi, rsi, rdx, rcx, r8, r9
Args (7+)     : spill ke stack (caller)
Stack align   : RSP ≡ 0 (mod 16) saat CALL dieksekusi
Red zone      : 128 byte di bawah RSP (tidak dipakai di v0.1)
```

### 3.1 Kewajiban caller

- Align RSP ke 16 byte **sebelum** `call`.
- Tidak mengasumsikan callee-saved register akan di-clobber.
- Jika ingin nilai register tertentu tetap tersedia setelah call,
  simpan dulu (push) atau copy ke callee-saved register.

### 3.2 Kewajiban callee (runtime)

- Push semua callee-saved yang akan dipakai di awal.
- Pop di akhir (restore).
- Saat memanggil fungsi lain (nested), pastikan RSP aligned.
- **Jangan** mengandalkan red zone di v0.1.

---

## 4. Kontrak Microsoft x64 (Windows) v0.1 — ✅ *Fase 11 completed*

```text
Caller-saved  : rax, rcx, rdx, r8, r9, r10, r11
Callee-saved  : rbx, rbp, rdi, rsi, r12, r13, r14, r15
Return        : rax
Args (1..4)   : rcx, rdx, r8, r9
Args (5+)     : spill ke stack (caller)
Stack align   : RSP ≡ 8 (mod 16) saat CALL dieksekusi
Shadow space  : 32 byte yang dicadangkan caller tepat di atas return address
```

Runtime Windows tersedia di `src/HlaX64.Runtime/windows-x64/`:
- `startup.nasm` — `_start` via `ExitProcess`
- `stdout.nasm` — `stdout_put_str`, `stdout_put_nl`, `stdout_put_int` via `WriteConsoleA`
- `conversion.nasm` — `int_to_str`

Compiler flag: `--target windows-x64-msabi`.

---

## 5. Default mode: `Library`

```bash
hla64 build examples/hello.hla64            # default = --runtime library
hla64 build examples/hello.hla64 --runtime library
```

Pada mode ini, semua helper seperti `stdout.put` di-link ke
`HlaX64.Runtime` (object/library). Compiler emit `extern` symbol dan
linker menggabungkan pada tahap link.

**Keuntungan**:

- Backend lebih sederhana (cukup emit `call`).
- Perilaku output konsisten.
- Test runtime terpisah dari test compiler.
- Port ke Windows hanya butuh module runtime baru.

## 6. Opsi: `Inline`

```bash
hla64 build examples/hello.hla64 --runtime inline
```

Pada mode ini, isi runtime di-inline langsung (mis. syscall `write`
disisipkan ke `.text`). Berguna untuk size-critical atau saat
library linking tidak diinginkan.

> **Peringatan**: tidak semua fungsi runtime layak di-inline (mis.
> `i64_to_str` cukup besar). Mode `inline` hanya boleh berisi fungsi
> yang disetujui explicit di tabel §1.

---

## 7. Acceptance criteria — ✅ *All met, Fase 9.5 completed*

- [x] Tiap fungsi runtime punya header `HLAX64-RUNTIME-FUNCTION v0.1` yang valid.
- [x] Tidak ada helper runtime tanpa `clobbers` dan `preserves` yang lengkap.
- [x] Runtime punya semantic version (di header).
- [x] Native integration test runtime berjalan di Linux CI dan lulus (16/16).
- [x] Saat menambah fungsi runtime baru, doc ini diupdate pada PR yang sama.
