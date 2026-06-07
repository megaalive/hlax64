; HlaX64 Runtime - Command-line argv (Linux x64 / System V ABI)
; Kernel layout at _start entry: [rsp]=argc, [rsp+8]=argv[0], ...
; SysVAbiLowerer calls hlax_argv_save_from_stack before the entry prologue.

bits 64
default rel

%define HLAX_ARGV_MAX 64

section .bss
    hlax_argc:        resd 1
    hlax_argv:        resq HLAX_ARGV_MAX
    hlax_argv_inited: resb 1

section .text

global hlax_argv_init
global hlax_argv_count
global hlax_argv_get
global hlax_argv_save_from_stack

; rdi = kernel stack pointer at _start (rsp before any pushes)
hlax_argv_save_from_stack:
    push rbx
    push rsi
    test rdi, rdi
    jz .empty
    mov eax, [rdi]
    test eax, eax
    js .empty
    cmp eax, HLAX_ARGV_MAX
    jbe .count_ok
    mov eax, HLAX_ARGV_MAX
.count_ok:
    mov [rel hlax_argc], eax
    xor ecx, ecx
    lea rsi, [rdi + 8]
    lea rbx, [rel hlax_argv]
.copy:
    cmp ecx, eax
    jge .finish
    mov rdx, [rsi + rcx * 8]
    mov [rbx + rcx * 8], rdx
    inc ecx
    jmp .copy
.empty:
    mov dword [rel hlax_argc], 0
.finish:
    mov byte [rel hlax_argv_inited], 1
    pop rsi
    pop rbx
    ret

hlax_argv_init:
    cmp byte [rel hlax_argv_inited], 0
    jne .done
    ; Without _start bootstrap, argv stays empty.
.done:
    ret

hlax_argv_count:
    cmp byte [rel hlax_argv_inited], 0
    jne .ready
    call hlax_argv_init
.ready:
    movsxd rax, dword [rel hlax_argc]
    ret

hlax_argv_get:
    cmp byte [rel hlax_argv_inited], 0
    jne .ready
    push rcx
    call hlax_argv_init
    pop rcx
.ready:
    movsxd rax, dword [rel hlax_argc]
    cmp rcx, rax
    jge .none
    lea r11, [rel hlax_argv]
    mov rax, [r11 + rcx * 8]
    ret
.none:
    xor rax, rax
    ret
