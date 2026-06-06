; HlaX64 Runtime - Startup (Linux x64 / System V ABI)
; File: startup.nasm
;
; This file provides the _start entry point and the newline constant
; used by the inline-emitted hello-world programs. The MVP compiler
; inlines equivalent code directly into the program output, but the
; functions here serve as a stable, linkable runtime for:
;   - C# interop (shared library exports)
;   - Procedure-aware compilation
;   - Manual assembly for testing
;
; Convention: System V ABI (Linux x64)

bits 64
default rel

; ---------------------------------------------------------------------
; Constants (in .rodata so they end up in a read-only section)
; ---------------------------------------------------------------------
section .rodata
    newline: db 0x0A

; ---------------------------------------------------------------------
; Code
; ---------------------------------------------------------------------
section .text

; _start: program entry point invoked by the Linux kernel.
; Expected stack: [argc, argv, envp] (System V ABI)
; rax must hold the desired exit code on entry to sys_exit.
global _start
_start:
    ; Set up a frame pointer for any runtime helpers called from here.
    push rbp
    mov  rbp, rsp

    ; The compiled program body is linked before this file, so by the
    ; time control reaches _start the program has already executed.
    ; The compiler is responsible for emitting the actual user code
    ; and jumping here only for finalization. For the MVP the
    ; compiler inlines everything in the main .text section, so
    ; _start is provided here for completeness when this runtime is
    ; linked as a shared library.
    mov  rdi, rax        ; exit code = whatever rax holds
    mov  rax, 60         ; sys_exit
    syscall
