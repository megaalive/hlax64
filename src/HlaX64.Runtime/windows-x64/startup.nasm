; HlaX64 Runtime - Startup (Windows x64 / MS ABI)
; File: startup.nasm
;
; Provides _start entry point for HlaX64 programs when linked as
; a standalone executable (--runtime library mode).
; On Windows, the entry point calls ExitProcess from kernel32.

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
; target:  windows-x64-msabi
; inputs:
;   (none — RSP is 16-byte aligned at entry per Windows loader)
; clobbers:
;   rax, rcx, rdx, r8, r9, r10, r11
; preserves:
;   (none — terminates the process)
; stack-align:
;   RSP ≡ 0 (mod 16) on entry (Windows loader guarantee)
; notes:
;   Program entry point invoked by the Windows PE loader.
;   The compiled program body sets rax = exit code (via ebx).
global _start
extern ExitProcess
_start:
    push rbp
    mov  rbp, rsp
    mov  ecx, eax
    call ExitProcess
