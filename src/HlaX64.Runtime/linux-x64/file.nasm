; HlaX64 Runtime - File helpers (Linux x64 / System V ABI)
; Thin wrappers around libc access/open/read/close. Link with -lc when used.

; HLAX64-RUNTIME-FUNCTION v0.1
; name:    hlax_path_exists
; target:  linux-x64-sysv
; inputs:  rdi = path (cstring)
; returns: rax = 1 if path exists, 0 otherwise
; clobbers: rax, rcx, rdx, rsi, rdi, r8, r9, r10, r11
; preserves: rbx, rbp, r12, r13, r14, r15

bits 64
default rel

extern access
extern open
extern read
extern close

section .text

global hlax_path_exists
global hlax_file_open_read
global hlax_file_read
global hlax_file_close

; rdi = path -> rax = 1 exists, 0 missing
hlax_path_exists:
    push rbx
    xor esi, esi
    call access
    pop rbx
    test eax, eax
    js .missing
    mov rax, 1
    ret
.missing:
    xor rax, rax
    ret

; rdi = path -> rax = fd or -1
hlax_file_open_read:
    push rbx
    xor esi, esi
    xor edx, edx
    call open
    pop rbx
    ret

; rdi = fd, rsi = buffer, rdx = count -> rax = bytes read or -1
hlax_file_read:
    jmp read

; rdi = fd -> rax = 0 on success
hlax_file_close:
    jmp close
