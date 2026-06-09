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
extern write
extern close

%define O_WRONLY   1
%define O_CREAT    64
%define O_TRUNC    512
%define S_IRUSR    64
%define S_IWUSR    128

section .text

global hlax_path_exists
global hlax_file_open_read
global hlax_file_open_write
global hlax_file_read
global hlax_file_write
global hlax_file_close
global hlax_stdout_write

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

; rdi = path -> rax = fd or -1
hlax_file_open_write:
    push rbx
    mov esi, O_WRONLY | O_CREAT | O_TRUNC
    mov edx, S_IRUSR | S_IWUSR
    call open
    pop rbx
    ret

; rdi = fd, rsi = buffer, rdx = count -> rax = bytes written or -1
hlax_file_write:
    jmp write

; rdi = buffer, rsi = count -> rax = bytes written to stdout or -1
hlax_stdout_write:
    mov rdx, rsi
    mov rsi, rdi
    mov edi, 1
    jmp write

; rdi = fd -> rax = 0 on success
hlax_file_close:
    jmp close
