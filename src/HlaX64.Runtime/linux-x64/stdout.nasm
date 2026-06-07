; HlaX64 Runtime - Standard Output (Linux x64 / System V ABI)
; File: stdout.nasm
;
; This file provides runtime functions for stdout output used by
; HlaX64 programs. Functions are callable from compiled code when
; linking with HlaX64.Runtime (--runtime library mode).

bits 64
default rel

; -----------------------------------------------------------------------
; Constants
; -----------------------------------------------------------------------
section .rodata
    newline: db 0x0A

section .bss
    intbuf: resb 24

; -----------------------------------------------------------------------
; Code
; -----------------------------------------------------------------------
section .text

; --- stdout_put_str ----------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_str
; target:  linux-x64-sysv
; inputs:
;   rdi = pointer to NUL-terminated string
; clobbers:
;   rax, rcx, rdx, rsi, rdi
; preserves:
;   rbx, rbp, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Calls sys_write(1, buf, len). fd=1 (stdout) must be open.
;   Leaf routine — no stack frame; SysV ABI requires RSP%16==0 before call,
;   but plain syscall does not use the call instruction stack push.
global stdout_put_str
stdout_put_str:
    mov  rsi, rdi        ; save string pointer (rdi = arg0)
    xor  rcx, rcx
.strlen_loop:
    mov  al, [rsi + rcx]
    test al, al
    jz   .strlen_done
    inc  rcx
    jmp  .strlen_loop
.strlen_done:
    mov  rax, 1
    mov  rdi, 1
    mov  rdx, rcx
    syscall
    ret

; --- stdout_put_nl ------------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_nl
; target:  linux-x64-sysv
; inputs:
;   (none)
; clobbers:
;   rax, rcx, rdx, rsi, rdi
; preserves:
;   rbx, rbp, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Prints a single newline (0x0A) to stdout via sys_write.
global stdout_put_nl
stdout_put_nl:
    mov  rax, 1
    mov  rdi, 1
    lea  rsi, [rel newline]
    mov  rdx, 1
    syscall
    ret

; --- stdout_put_int -----------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    stdout_put_int
; target:  linux-x64-sysv
; inputs:
;   rdi = signed 64-bit integer value
; clobbers:
;   rax, rcx, rdx, rsi, rdi
; preserves:
;   rbx, rbp, r12, r13, r14, r15
; stack-align:
;   caller-rsp-mod-16 == 0 on entry
; notes:
;   Converts rdi to decimal via int_to_str, then prints via stdout_put_str.
global stdout_put_int
stdout_put_int:
    lea  rsi, [rel intbuf]
    call int_to_str
    mov  rdi, rax
    jmp  stdout_put_str
