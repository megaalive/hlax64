; HlaX64 Runtime - String/memory helpers (Linux x64 / System V ABI)

bits 64
default rel

section .text

global hlax_strlen
global hlax_memcpy
global hlax_memset
global hlax_is_space

; rdi = cstring -> rax = length (bytes before NUL)
hlax_strlen:
    xor rax, rax
.scan:
    cmp byte [rdi + rax], 0
    je .done
    inc rax
    jmp .scan
.done:
    ret

; rdi = dst, rsi = src, rdx = count -> rax = dst
hlax_memcpy:
    mov rax, rdi
    mov rcx, rdx
    test rcx, rcx
    jz .done
.copy:
    mov al, [rsi]
    mov [rdi], al
    inc rsi
    inc rdi
    dec rcx
    jnz .copy
.done:
    ret

; rdi = dst, rsi = fill byte, rdx = count -> rax = dst
hlax_memset:
    mov rax, rdi
    mov rcx, rdx
    mov al, sil
    test rcx, rcx
    jz .done
.fill:
    mov [rdi], al
    inc rdi
    dec rcx
    jnz .fill
.done:
    ret

; rdi = byte value -> rax = 1 if whitespace, else 0
hlax_is_space:
    mov rax, rdi
    cmp rax, 32
    je .yes
    cmp rax, 9
    je .yes
    cmp rax, 10
    je .yes
    cmp rax, 13
    je .yes
    xor rax, rax
    ret
.yes:
    mov rax, 1
    ret
