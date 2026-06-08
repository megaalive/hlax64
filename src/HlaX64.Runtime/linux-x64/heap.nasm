; HlaX64 Runtime - Heap helpers (Linux x64 / System V ABI)
; Thin wrappers around libc malloc/realloc/free. Link with -lc when used.

bits 64
default rel

extern malloc
extern realloc
extern free

section .text

global hlax_malloc
global hlax_realloc
global hlax_free

; rdi = byte count
hlax_malloc:
    jmp malloc

; rdi = pointer, rsi = new byte count
hlax_realloc:
    jmp realloc

; rdi = pointer
hlax_free:
    jmp free
