; HlaX64 Runtime - Integer-to-String Conversion (Linux x64 / System V ABI)
; File: conversion.nasm
;
; Provides int_to_str: convert a signed 64-bit integer to a NUL-
; terminated decimal string in a caller-supplied buffer.
;
;   rdi = signed 64-bit integer value
;   rsi = pointer to output buffer (must be at least 21 bytes:
;         19 digits + optional sign + NUL)
;   rax = pointer to the start of the written string (i.e. rsi)
;
; Used by procedure-aware compilation and the shared library that
; backs C# interop. The MVP emitter inlines an equivalent algorithm
; inside EmitStdoutPut for the register case.

bits 64
default rel

; ---------------------------------------------------------------------
; int_to_str
; ---------------------------------------------------------------------
global int_to_str
int_to_str:
    push rbx
    push r12
    mov  r12, rsi            ; r12 = buffer start (return value)
    mov  rax, rdi            ; rax = value
    xor  rcx, rcx            ; rcx = digit count
    mov  rbx, 10

    ; Handle zero
    test rax, rax
    jnz  .check_negative
    mov  byte [rsi], '0'
    mov  byte [rsi + 1], 0
    jmp  .done

.check_negative:
    test rax, rax
    jns  .div_loop
    ; Negative: write '-' and negate
    mov  byte [rsi], '-'
    inc  rsi
    neg  rax

.div_loop:
    xor  rdx, rdx
    div  rbx                 ; rax = rax/10, rdx = digit
    add  rdx, '0'
    mov  [rsi + rcx], dl
    inc  rcx
    test rax, rax
    jnz  .div_loop

    ; Digits are in reverse order. Reverse in place.
    ; rsi points to first digit, rcx = count
    mov  r8, rsi
    add  r8, rcx
    dec  r8                  ; r8 = last digit index
.reverse_loop:
    cmp  rsi, r8
    jge  .nul_terminate
    mov  al, [rsi]
    mov  bl, [r8]
    mov  [rsi], bl
    mov  [r8], al
    inc  rsi
    dec  r8
    jmp  .reverse_loop

.nul_terminate:
    ; rsi currently points one past the end of the digits
    mov  byte [rsi], 0

.done:
    mov  rax, r12            ; return buffer start
    pop  r12
    pop  rbx
    ret
