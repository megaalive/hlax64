; HlaX64 Runtime - String/memory helpers (Windows x64 / MS ABI)

bits 64
default rel

section .text

global hlax_strlen
global hlax_memcpy
global hlax_memset
global hlax_is_space

; rcx = cstring -> rax = length
hlax_strlen:
    xor rax, rax
    test rcx, rcx
    jz .done
    mov rdi, rcx
.scan:
    cmp byte [rdi + rax], 0
    je .done
    inc rax
    jmp .scan
.done:
    ret

; rcx = dst, rdx = src, r8 = count -> rax = dst
hlax_memcpy:
    push rdi
    push rsi
    mov rax, rcx
    mov rdi, rcx
    mov rsi, rdx
    mov rcx, r8
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
    pop rsi
    pop rdi
    ret

; rcx = dst, rdx = fill byte, r8 = count -> rax = dst
hlax_memset:
    push rdi
    mov rax, rcx
    mov rdi, rcx
    mov al, dl
    mov rcx, r8
    test rcx, rcx
    jz .done
.fill:
    mov [rdi], al
    inc rdi
    dec rcx
    jnz .fill
.done:
    pop rdi
    ret

; rcx = byte value -> rax = 1 if whitespace, else 0
hlax_is_space:
    mov rax, rcx
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
