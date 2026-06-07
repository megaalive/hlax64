; HlaX64 Runtime - Command-line argv (Windows x64 / MS ABI)
; Parses GetCommandLineA in-place. hlax_argv_get returns ANSI cstring pointers.
; Note: use lea+index for hlax_argv — [rel hlax_argv + reg*8] is invalid in win64 rel mode.

bits 64
default rel

%define HLAX_ARGV_MAX 64

section .bss
    hlax_argc:        resd 1
    hlax_argv:        resq HLAX_ARGV_MAX
    hlax_argv_inited: resb 1

section .text

extern GetCommandLineA

global hlax_argv_init
global hlax_argv_count
global hlax_argv_get

hlax_argv_init:
    push rbx
    push rsi
    push rdi
    push r12
    push r13
    push r14
    push r15
    sub rsp, 48

    cmp byte [rel hlax_argv_inited], 0
    jne .done

    lea r13, [rel hlax_argv]

    call GetCommandLineA
    mov rsi, rax
    test rsi, rsi
    jz .finish

    xor r12d, r12d

.skip_ws:
    movzx eax, byte [rsi]
    test al, al
    jz .finish
    cmp al, 32
    je .skip_ws_next
    cmp al, 9
    je .skip_ws_next
    jmp .parse_token

.skip_ws_next:
    inc rsi
    jmp .skip_ws

.parse_token:
    cmp r12d, HLAX_ARGV_MAX
    jae .finish
    mov eax, r12d
    mov [r13 + rax * 8], rsi
    inc r12d

    movzx eax, byte [rsi]
    cmp al, 34
    je .quoted

.scan_unquoted:
    movzx eax, byte [rsi]
    test al, al
    jz .token_done
    cmp al, 32
    je .terminate_token
    cmp al, 9
    je .terminate_token
    inc rsi
    jmp .scan_unquoted

.quoted:
    inc rsi
.scan_quoted:
    movzx eax, byte [rsi]
    test al, al
    jz .token_done
    cmp al, 34
    je .close_quote
    inc rsi
    jmp .scan_quoted
.close_quote:
    mov byte [rsi], 0
    inc rsi
    jmp .after_token

.terminate_token:
    mov byte [rsi], 0
    inc rsi
    jmp .after_token

.token_done:
    jmp .finish

.after_token:
    movzx eax, byte [rsi]
    test al, al
    jz .finish
    cmp al, 32
    je .skip_between
    cmp al, 9
    je .skip_between
    jmp .parse_token

.skip_between:
    inc rsi
    movzx eax, byte [rsi]
    test al, al
    jz .finish
    cmp al, 32
    je .skip_between
    cmp al, 9
    je .skip_between
    jmp .parse_token

.finish:
    mov [rel hlax_argc], r12d
    mov byte [rel hlax_argv_inited], 1

.done:
    add rsp, 48
    pop r15
    pop r14
    pop r13
    pop r12
    pop rdi
    pop rsi
    pop rbx
    ret

hlax_argv_count:
    cmp byte [rel hlax_argv_inited], 0
    jne .ready
    sub rsp, 40
    call hlax_argv_init
    add rsp, 40
.ready:
    movsxd rax, dword [rel hlax_argc]
    ret

hlax_argv_get:
    cmp byte [rel hlax_argv_inited], 0
    jne .ready
    push rcx
    sub rsp, 40
    call hlax_argv_init
    add rsp, 40
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
