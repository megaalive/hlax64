# HlaX64 patterns & cookbook

**Start here when builds fail or results look wrong.** These are the idioms the compiler and runtime expect — not optional style tips.

Related: [diagnostics.md](diagnostics.md) · [install.md](install.md) · [language-spec.md](language-spec.md) · [memory-and-bounds.md](memory-and-bounds.md) · [Project Euler patterns](../examples/project-euler/docs/patterns.md)

---

## 1. Golden rule: operand order

HlaX64 uses **HLA order** (opposite of NASM text):

```text
mov(source, destination)   →   destination = source
add(source, destination)   →   destination += source
```

Examples:

```hla64
mov(10, rax);      // rax = 10
mov(rax, rbx);     // rbx = rax
add(1, counter);   // counter += 1
```

If `mov(a, b)` “does the wrong thing”, you probably swapped source and destination.

---

## 2. Process exit code

| Target | Register read at exit |
|--------|------------------------|
| **Windows** (`windows-x64-msabi`) | **`rbx`** → `ExitProcess` |
| **Linux** (`linux-x64-sysv`) | **`rax`**, unless you wrote **`rbx`** (then `rbx` wins) |

Cross-platform programs:

```hla64
begin myprog;
    mov(42, rbx);
    mov(rbx, rax);   // Linux fallback when rbx was not otherwise used
end myprog;
```

Windows-only:

```hla64
mov(answer, rbx);
```

Curriculum: `examples/curriculum/00-getting-started/exitcode.hla64` (use `rbx` on Windows).

---

## 3. Build checklist (friends hitting “random” link/NASM errors)

Run **`hla64 doctor`** first. Then:

| Step | Command | Typical failure |
|------|---------|-----------------|
| Target | `hla64 build file.hla64 --target windows-x64-msabi` | Wrong ABI / missing `kernel32` |
| NASM | `nasm -v` on PATH | `nasm: not found` |
| Linker (Win) | LLVM `lld-link` or MSVC `link.exe` | “No Windows linker found” |
| Linker (Linux) | `gcc` | undefined reference to runtime |
| Runtime | `#include("stdlib64.hhf")` when using `stdout.put` | link errors for `stdout_put_*` |
| Path | build from repo root or pass full paths | file not found |

More: [install.md — Troubleshooting](install.md#troubleshooting).

---

## 4. Common compile / NASM errors

| Symptom | Likely cause | Fix |
|---------|--------------|-----|
| `invalid segment override` / `str:...` in NASM | String literal passed to a normal `call` | Use `call proc(&label)` or static string; see §8 |
| `unknown instruction` | Typo or unsupported mnemonic | Stick to [language-spec](language-spec.md) table |
| `Expected 'RightParen'` | `while(x >= 0)` — no `>=` in `while` yet | Use `while(x > -1)` or restructure |
| HLAX0004 wrong operand count | `mod(3, rax)` needs register divisor | `mov(3, r10); mod(r10, rax)` |
| Link: undefined `stdout_put_*` | Missing stdlib include / runtime objects | `#include("stdlib64.hhf")` + link with runtime |
| Program runs, exit code always **0** on Windows | Result left in `rax` not `rbx` | §2 |
| `stdout.put` shows garbage for locals | Passed stack slot name wrong on Windows | Load into a register first (§7) |
| Crash / wrong large `static` values | See §6 | Use register indices; large literals via register |

Regression harness: `examples/qa/bug-farm/idiv-loop/`.

---

## 5. Integer division (`idiv`) and remainder (`mod`)

Dividend must be in **`rax`**. **`idiv` clobbers `rax` (quotient) and `rdx` (remainder).**

```hla64
mov(n, rax);
mov(k, r11);       // divisor MUST be a register
idiv(r11, rax);    // rax = n / k  (signed quotient)

mov(n, rax);
mod(r11, rax);     // rax = n % k  (signed remainder)
```

Loop tips:

- Reload the dividend into `rax` before each `idiv`/`mod`.
- Keep the **divisor** in a dedicated register (e.g. `r15`) across a Lehmer chain — do not reuse it as loop index.
- Keep loop limits in **`r9`** only if you accept that **array access lowering may clobber caller-saved scratch regs** (`r10`, `r11`, `r8`) — see §6.

Curriculum: `examples/curriculum/03-control-flow/idiv-jmp.hla64`.

---

## 6. Static / global arrays (`static` block)

```hla64
static
    table: int64[10];
endstatic;

begin demo;
    mov(362880, table[3]);   // store
    mov(table[3], rbx);      // load — Windows exit in rbx
end demo;
```

Rules:

- **Element type matters** — `int64[N]` is 8 bytes per element; `byte[N]` is 1 byte.
- **Index can be register or literal** — `table[i]` with `i` in `r10` is fine.
- **Large immediates** (>32767): load via register first: `mov(999999, r10); mov(r10, r12);`
- **Compiler scratch:** indexed global loads/stores use `lea base, [rel name]` — **`r10`/`r11`/`r8` may be overwritten** during that instruction sequence. Copy live values (e.g. Lehmer `idx[pos]` into **`r12`**) before nested loops that touch arrays.

Avoid mem–mem: bounce through a register (`mov(a, rax); mov(rax, b)`).

---

## 7. `stdout.put` and `stdout.putu`

```hla64
#include("stdlib64.hhf")

begin demo;
    mov(123, rdx);
    stdout.put("Answer: ", rdx, nl);
end demo;
```

| Argument | Behavior |
|----------|----------|
| `"string"` | string literal |
| `nl` | newline (from stdlib) |
| register (`rdx`, `r12`, …) | print integer (**preferred on Windows**) |
| local name | works for some setups; **prefer register** if output is wrong |

**Avoid** `stdout.put` inside tight numeric loops — runtime calls clobber scratch state.

Unsigned decimal: `stdout.putu(...)`.

---

## 8. Procedure calls & string arguments

```hla64
extern procedure CreateFileA(...): int64 from "kernel32.dll";

static
    path: byte[] := "test.txt", 0;
endstatic;

begin demo;
    call read_file(&path);           // address of static buffer
    // OR (compiler support):
    call read_file("test.txt");      // string literal → rodata label
end demo;
```

Win32 **`extern`** calls: load arguments into **`rcx`, `rdx`, `r8`, `r9`**, then stack — compiler handles this when you use `call CreateFileA(...)`.

Tools example: `examples/tools/10-windows/linecount/linecount.hla64` (argv + `CreateFileA`).

---

## 9. `imul` (multiply)

```hla64
mov(digit, rax);
mov(1, r14);
imul(rax, r14);    // r14 *= rax   (product stays in r14)
```

Do **not** `mov(rax, r14)` after `imul` unless you intend to copy — the product is already in the **second** operand (destination).

---

## 10. Registers quick reference

| Register | Typical use in examples | Clobbered by |
|----------|-------------------------|--------------|
| `rax` | `idiv` dividend / result | `idiv`, calls |
| `rdx` | `idiv` remainder | `idiv` |
| `rbx` | **Windows exit code** | keep for exit |
| `r9` | outer loop bound | avoid reuse after array ops if possible |
| `r10`–`r11` | scratch, array index prep | **global `arr[i]` access** |
| `r12`–`r15` | callee-saved in ABI — good for long-lived temps | calls (if not saved) |

When in doubt, copy a live value to **`r12`/`r13`/`r14`/`r15`** before a complex instruction sequence.

---

## 11. Learn by example (curriculum path)

| Topic | Path |
|-------|------|
| Hello / exit | `examples/curriculum/00-getting-started/` |
| Arithmetic | `examples/curriculum/01-arithmetic/` |
| `idiv` / labels | `examples/curriculum/03-control-flow/idiv-jmp.hla64` |
| Static / globals | `examples/curriculum/05-memory/global-counter.hla64` |
| Arrays | `examples/curriculum/05-memory/array-max.hla64` |
| Win32 files | `examples/tools/10-windows/linecount/` |
| Division stress tests | `examples/qa/bug-farm/idiv-loop/` |

Playground (browser): [megaalive.github.io/hlax64/playground](https://megaalive.github.io/hlax64/playground/index.html).

---

## 13. Stdlib file and string helpers

Link runtime library procedures declared in [`stdlib64.hhf`](../src/HlaX64.Runtime/include/stdlib64.hhf):

| API | Purpose |
|-----|---------|
| `hlax_path_exists(path)` | Returns 1 when path exists |
| `hlax_file_open_read(path)` | Open for read; returns handle/fd or -1 |
| `hlax_file_read(handle, buf, count)` | Read bytes into buffer |
| `hlax_file_close(handle)` | Close handle/fd |
| `hlax_strlen`, `hlax_memcpy`, `hlax_memset`, `hlax_is_space` | String/memory helpers |
| `hlax_getpid`, `hlax_hostname`, `hlax_mem_*`, `hlax_file_size` | Systems introspection — [`runtime-contract.md`](runtime-contract.md) |
| `hlax_os_last_error`, `hlax_cpu_count`, `hlax_disk_*`, `hlax_self_rss_bytes` | Resource reporting |
| `hlax_net_*`, `hlax_dns_resolve_v4`, `hlax_tcp_*` | IPv4 TCP/DNS (local fixture tests) — [`07-systems-networking.md`](tutorials/07-systems-networking.md) |

Example (`exists` tool):

```hla64
extern procedure hlax_path_exists(path: cstring): int64;
call hlax_argv_get(1);
call hlax_path_exists(rax);
if(rax = 0) then
    stdout.put("missing", nl);
endif;
```

See `examples/tools/10-windows/exists/` and `examples/tools/12-linux/exists/`.

---

## 12. Still stuck?

1. `hla64 explain yourfile.hla64` — IR + NASM preview  
2. `hla64 build yourfile.hla64 --target …` — read NASM line from error  
3. Compare with the closest curriculum example above  
4. [GitHub Issues](https://github.com/megaalive/hlax64/issues) — include `hla64 doctor` output and the first failing command  

Advanced / Project Euler–specific idioms: [examples/project-euler/docs/patterns.md](../examples/project-euler/docs/patterns.md).
