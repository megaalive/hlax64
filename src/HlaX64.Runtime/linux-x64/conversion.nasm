; HlaX64 Runtime - Integer-to-String Conversion (Linux x64 / System V ABI)
; File: conversion.nasm
;
; Provides int_to_str: convert a signed 64-bit integer to decimal ASCII
; string. Used by runtime helpers and procedure-aware compilation.

bits 64
default rel

; -----------------------------------------------------------------------
; Code
; -----------------------------------------------------------------------
section .text

; --- int_to_str ---------------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    int_to_str
; target:  linux-x64-sysv
; inputs:
;   rdi = signed 64-bit integer value
;   rsi = pointer to output buffer (>= 21 bytes)
; returns:
;   rax = pointer to start of written string (same as rsi)
; clobbers:
;   rax, rcx, rdx, rsi, r8, rbx, r12
; preserves:
;   rbp, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Buffer must be at least 21 bytes (19 digits + sign + NUL).
;   Preserves callee-saved rbx and r12.
global int_to_str
int_to_str:
    push rbx
    push r12
    mov  r12, rsi
    mov  rax, rdi
    xor  rcx, rcx
    mov  rbx, 10

    test rax, rax
    jnz  .check_negative
    mov  byte [rsi], '0'
    mov  byte [rsi + 1], 0
    jmp  .done

.check_negative:
    test rax, rax
    jns  .div_loop
    mov  byte [rsi], '-'
    inc  rsi
    neg  rax

.div_loop:
    xor  rdx, rdx
    div  rbx
    add  rdx, '0'
    mov  [rsi + rcx], dl
    inc  rcx
    test rax, rax
    jnz  .div_loop

    mov  r8, rsi
    add  r8, rcx
    dec  r8
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
    mov  byte [rsi], 0

.done:
    mov  rax, r12
    pop  r12
    pop  rbx
    ret

; --- uint_to_str --------------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    uint_to_str
; target:  linux-x64-sysv
; inputs:
;   rdi = unsigned 64-bit integer value (bit pattern)
;   rsi = pointer to output buffer (>= 21 bytes)
; returns:
;   rax = pointer to start of written string (same as rsi)
global uint_to_str
uint_to_str:
    push rbx
    push r12
    mov  r12, rsi
    mov  r9, rsi
    mov  rax, rdi
    xor  rcx, rcx
    mov  rbx, 10

    test rax, rax
    jnz  .div_loop
    mov  byte [r9], '0'
    mov  byte [r9 + 1], 0
    jmp  .done_u

.div_loop:
    xor  rdx, rdx
    div  rbx
    add  rdx, '0'
    mov  [r9 + rcx], dl
    inc  rcx
    test  rax, rax
    jnz  .div_loop

    mov  r10, r9
    mov  r8, r9
    add  r8, rcx
    dec  r8
.reverse_loop_u:
    cmp  r9, r8
    jge  .nul_terminate_u
    mov  al, [r9]
    mov  bl, [r8]
    mov  [r9], bl
    mov  [r8], al
    inc  r9
    dec  r8
    jmp  .reverse_loop_u

.nul_terminate_u:
    mov  byte [r10 + rcx], 0

.done_u:
    mov  rax, r12
    pop  r12
    pop  rbx
    ret
