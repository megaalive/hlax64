; HlaX64 Runtime - Standard Output (Windows x64 / MS ABI)
; File: stdout.nasm
;
; Provides runtime functions for stdout output on Windows x64.
; Uses Win32 API (GetStdHandle, WriteConsoleA) from kernel32.

bits 64
default rel

; -----------------------------------------------------------------------
; Constants
; -----------------------------------------------------------------------
section .rodata
    newline: db 0x0A

section .bss
    intbuf: resb 24
    written: resq 1

; -----------------------------------------------------------------------
; Code
; -----------------------------------------------------------------------
section .text

; --- stdout_put_str ----------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_str
; target:  windows-x64-msabi
; inputs:
;   rcx = pointer to NUL-terminated string
; clobbers:
;   rax, rcx, rdx, r8, r9, r10, r11
; preserves:
;   rbx, rbp, rdi, rsi, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Uses WriteConsoleA to print to stdout.
global stdout_put_str
extern GetStdHandle
extern WriteConsoleA
stdout_put_str:
    push rbx
    push rsi
    push rdi
    sub  rsp, 32         ; shadow space

    mov  rbx, rcx        ; save string pointer
    ; Compute string length
    xor  rcx, rcx
.strlen_loop:
    mov  al, [rbx + rcx]
    test al, al
    jz   .strlen_done
    inc  rcx
    jmp  .strlen_loop
.strlen_done:
    mov  rsi, rcx        ; save length in rsi

    ; GetStdHandle(STD_OUTPUT_HANDLE = -11)
    mov  ecx, -11
    call GetStdHandle

    ; WriteConsoleA(hConsole, lpBuffer, nChars, &written, NULL)
    mov  rcx, rax        ; hConsole
    mov  rdx, rbx        ; lpBuffer
    mov  r8, rsi         ; nChars
    lea  r9, [rel written] ; &written
    mov  qword [rsp+32], 0 ; lpReserved = NULL
    call WriteConsoleA

    add  rsp, 32
    pop  rdi
    pop  rsi
    pop  rbx
    ret

; --- stdout_put_nl ------------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_nl
; target:  windows-x64-msabi
; inputs:
;   (none)
; clobbers:
;   rax, rcx, rdx, r8, r9, r10, r11
; preserves:
;   rbx, rbp, rdi, rsi, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Prints a single newline (0x0A) to stdout via WriteConsoleA.
global stdout_put_nl
stdout_put_nl:
    push rbp
    mov  rbp, rsp
    sub  rsp, 32

    mov  ecx, -11          ; STD_OUTPUT_HANDLE
    call GetStdHandle

    mov  rcx, rax          ; hConsole
    lea  rdx, [rel newline]; lpBuffer
    mov  r8d, 1            ; nChars
    lea  r9, [rel written] ; &written
    mov  qword [rsp+32], 0 ; lpReserved = NULL
    call WriteConsoleA

    add  rsp, 32
    pop  rbp
    ret

; --- stdout_put_int -----------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_int
; target:  windows-x64-msabi
; inputs:
;   rcx = signed 64-bit integer value
; clobbers:
;   rax, rcx, rdx, r8, r9, r10, r11
; preserves:
;   rbx, rbp, rdi, rsi, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Converts rcx to decimal via int_to_str, then prints via stdout_put_str.
global stdout_put_int
stdout_put_int:
    lea  rdx, [rel intbuf]
    call int_to_str
    mov  rcx, rax
    jmp  stdout_put_str
