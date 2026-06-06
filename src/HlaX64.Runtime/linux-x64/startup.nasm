; HlaX64 Runtime - Startup (Linux x64 / System V ABI)
; File: startup.nasm
;
; Provides _start entry point for HlaX64 programs when linked as
; a standalone executable (--runtime library mode).

bits 64
default rel

; -----------------------------------------------------------------------
; Constants
; -----------------------------------------------------------------------
section .rodata
    newline: db 0x0A

; -----------------------------------------------------------------------
; Code
; -----------------------------------------------------------------------
section .text

; --- _start -------------------------------------------------------------
; HLAX64-RUNTIME-FUNCTION v0.1
; name:    _start
; target:  linux-x64-sysv
; inputs:
;   (none — kernel interface: argc, argv, envp on stack)
; clobbers:
;   rax, rdi
; preserves:
;   (none — terminates the process)
; stack-align:
;   RSP ≡ 8 (mod 16) on entry (kernel guarantee)
; notes:
;   Program entry point invoked by the Linux kernel.
;   Expects the compiled program body to set rax = exit code.
global _start
_start:
    push rbp
    mov  rbp, rsp
    mov  rdi, rax
    mov  rax, 60
    syscall
