; HlaX64 Runtime - Heap helpers (Windows x64 / MS ABI)
; Uses HeapAlloc/HeapReAlloc/HeapFree via GetProcessHeap (kernel32, no CRT).

bits 64
default rel

%define HEAP_ZERO_MEMORY 8

extern GetProcessHeap
extern HeapAlloc
extern HeapReAlloc
extern HeapFree

section .text

global hlax_malloc
global hlax_realloc
global hlax_free

; rcx = byte count
hlax_malloc:
    sub rsp, 40
    mov r8, rcx
    call GetProcessHeap
    mov rcx, rax
    mov edx, HEAP_ZERO_MEMORY
    call HeapAlloc
    add rsp, 40
    ret

; rcx = pointer, rdx = new byte count
hlax_realloc:
    sub rsp, 48
    mov [rsp+32], rcx
    mov [rsp+40], rdx
    call GetProcessHeap
    mov rcx, rax
    mov r8, [rsp+32]
    test r8, r8
    jz .alloc
    xor edx, edx
    mov r9, [rsp+40]
    call HeapReAlloc
    jmp .done
.alloc:
    mov edx, HEAP_ZERO_MEMORY
    mov r8, [rsp+40]
    call HeapAlloc
.done:
    add rsp, 48
    ret

; rcx = pointer
hlax_free:
    sub rsp, 40
    mov r8, rcx
    call GetProcessHeap
    mov rcx, rax
    xor edx, edx
    call HeapFree
    add rsp, 40
    ret
