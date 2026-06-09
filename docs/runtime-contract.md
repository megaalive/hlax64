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
| `hla64_str_len`        | `mem.nasm`                   | panjang string (nol-terminated) | shipped (`hlax_strlen`) |
| `hla64_memcpy`         | `mem.nasm`                   | salin byte | shipped (`hlax_memcpy`) |
| `hla64_memset`         | `mem.nasm`                   | isi byte | shipped (`hlax_memset`) |
| `hlax_path_exists`     | `file.nasm`                  | cek path ada | shipped |
| `hlax_file_open_read`  | `file.nasm`                  | buka file read-only | shipped |
| `hlax_file_open_write` | `file.nasm`                  | buka file write (create/truncate) | shipped |
| `hlax_file_read`       | `file.nasm`                  | baca chunk ke buffer | shipped |
| `hlax_file_write`      | `file.nasm`                  | tulis chunk dari buffer | shipped |
| `hlax_file_close`      | `file.nasm`                  | tutup handle/fd | shipped |
| `hlax_stdout_write`    | `file.nasm`                  | tulis raw bytes ke stdout | shipped |
| `hlax_is_printable`    | `mem.nasm`                   | ASCII printable 0x20..0x7E | shipped |
| `hlax_getpid`          | `sys.nasm`                   | current process id | shipped |
| `hlax_hostname`        | `sys.nasm`                   | machine hostname into buffer | shipped |
| `hlax_uptime_secs`     | `sys.nasm`                   | seconds since boot | shipped |
| `hlax_mem_total`       | `sys.nasm`                   | total physical RAM bytes | shipped |
| `hlax_mem_avail`       | `sys.nasm`                   | available physical RAM bytes | shipped |
| `hlax_file_size`       | `sys.nasm`                   | file size bytes for path | shipped |
| `hlax_os_last_error`   | `sys.nasm`                   | last OS error (`errno` / `GetLastError`) | shipped |
| `hlax_cpu_count`       | `sys.nasm`                   | online CPU count | shipped |
| `hlax_disk_total_bytes`| `sys.nasm`                   | total disk bytes for mount of path | shipped |
| `hlax_disk_avail_bytes`| `sys.nasm`                   | available disk bytes for mount of path | shipped |
| `hlax_self_rss_bytes`  | `sys.nasm`                   | current process RSS bytes | shipped (Windows: `psapi.lib`) |
| `hlax_load_avg_milli`  | `sys.nasm`                   | 1-minute load ×1000 (Linux only) | shipped (Windows: `-1`) |
| `hlax_net_init`        | `net.nasm`                   | Winsock startup (Windows) / no-op (Linux) | shipped |
| `hlax_net_last_error`  | `net.nasm`                   | last socket error (`errno` / `WSAGetLastError`) | shipped |
| `hlax_dns_resolve_v4`  | `net.nasm`                   | resolve hostname → dotted IPv4 string | shipped |
| `hlax_tcp_connect`     | `net.nasm`                   | TCP connect IPv4 literal host | shipped |
| `hlax_tcp_connect_name`| `net.nasm`                   | TCP connect via DNS (IPv4) | shipped |
| `hlax_tcp_connect_timeout` | `net.nasm`               | connect with timeout (ms) | shipped |
| `hlax_tcp_set_timeouts_ms` | `net.nasm`               | recv/send socket timeouts (ms) | shipped |
| `hlax_tcp_write`       | `net.nasm`                   | send bytes on socket | shipped |
| `hlax_tcp_write_all`   | `net.nasm`                   | send all bytes (partial send loop) | shipped |
| `hlax_tcp_read`        | `net.nasm`                   | recv bytes from socket | shipped |
| `hlax_tcp_read_once`   | `net.nasm`                   | explicit alias of recv | shipped |
| `hlax_tcp_close`       | `net.nasm`                   | close socket (`0` ok, `-1` fail) | shipped |
| `hla64_exit`           | `startup.nasm`               | syscall `exit(rdi)` | MVP |

Windows `hlax_file_read` accepts handle `0` (`stdin_fd`) and reads from `GetStdHandle(STD_INPUT_HANDLE)` so `tee` works with piped stdin.

**System runtime (`sys.nasm`)** — Linux uses libc (`getpid`, `gethostname`, `sysinfo`, `stat`, `sysconf`, `statvfs`, `getrusage`); Windows uses `kernel32` + `psapi` for RSS. Uptime on Windows is tick-based (`GetTickCount64 / 1000`), not wall-clock boot time. `hlax_load_avg_milli` returns `-1` on Windows (unsupported).

**Network runtime (`net.nasm`)** — IPv4 literal and DNS hostname connect, socket timeouts, reliable `write_all`. Linux links libc socket/DNS functions; Windows links `ws2_32.lib`. TLS, ICMP, and IPv6 are out of scope.

### Tool exit-code convention (examples/tools)

| Exit | Meaning |
|------|---------|
| `0` | success |
| `1` | usage / bad argv |
| `2` | OS/system failure (`hlax_os_last_error`) |
| `3` | network failure (`hlax_net_last_error`) |

On failure, tools print `err=<code>` when a last-error helper applies. Runtime helpers still return `-1` on failure; see `HLAX_ERR_*` constants in `stdlib64.hhf`.

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
