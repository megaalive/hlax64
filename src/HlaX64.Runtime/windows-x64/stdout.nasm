; HlaX64 Runtime - Standard Output (Windows x64 / MS ABI)
; File: stdout.nasm
;
; Provides runtime functions for stdout output on Windows x64.
; Uses Win32 API (GetStdHandle, WriteFile) from kernel32.

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
;   Uses WriteFile to print to stdout.
global stdout_put_str
extern GetStdHandle
extern WriteFile
stdout_put_str:
    push rbx
    push rsi
    push rdi
    sub  rsp, 48         ; 32-byte shadow + 8 for 5th arg (+ 16-byte alignment)

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
    mov  rcx, -11
    call GetStdHandle

    ; WriteFile(hFile, lpBuffer, nBytes, lpNumberOfBytesWritten, lpOverlapped)
    mov  rcx, rax        ; hFile
    mov  rdx, rbx        ; lpBuffer
    mov  r8, rsi         ; nBytes
    lea  r9, [rel written] ; lpNumberOfBytesWritten
    mov  qword [rsp+32], 0 ; lpOverlapped = NULL
    call WriteFile

    add  rsp, 48
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
;   Prints a single newline (0x0A) to stdout via WriteFile.
global stdout_put_nl
stdout_put_nl:
    push rbp
    mov  rbp, rsp
    sub  rsp, 48         ; 32-byte shadow + 8 for 5th arg (+ 16-byte alignment)

    mov  rcx, -11          ; STD_OUTPUT_HANDLE
    call GetStdHandle

    mov  rcx, rax          ; hFile
    lea  rdx, [rel newline]; lpBuffer
    mov  r8d, 1            ; nBytes
    lea  r9, [rel written] ; lpNumberOfBytesWritten
    mov  qword [rsp+32], 0 ; lpOverlapped = NULL
    call WriteFile

    add  rsp, 48
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
extern int_to_str
global stdout_put_int
stdout_put_int:
    push rbp
    mov  rbp, rsp
    sub  rsp, 48         ; shadow + alignment for int_to_str / WriteFile calls
    lea  rdx, [rel intbuf]
    call int_to_str
    mov  rcx, rax
    call stdout_put_str
    add  rsp, 48
    pop  rbp
    ret

; --- stdout_put_uint ----------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_uint
; target:  windows-x64-msabi
; inputs:
;   rcx = unsigned 64-bit integer value (bit pattern)
extern uint_to_str
global stdout_put_uint
stdout_put_uint:
    push rbp
    mov  rbp, rsp
    sub  rsp, 48
    lea  rdx, [rel intbuf]
    call uint_to_str
    mov  rcx, rax
    call stdout_put_str
    add  rsp, 48
    pop  rbp
    ret
