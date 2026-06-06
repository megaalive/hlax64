; HlaX64 Runtime - Standard Output (Linux x64 / System V ABI)
; File: stdout.nasm
;
; Stable runtime API used by:
;   - procedure-aware compilation (post-Fase 6)
;   - C# interop shared library (Fase 12)
;
; The MVP emitter inlines equivalent sys_write sequences directly
; into the .text section; this file provides the same behaviour as
; a reusable function so user code can call stdout_put_str etc.
;
; ABI: System V. All registers except rbx, rbp, r12-r15 are caller-
; saved; we therefore preserve nothing special here.

bits 64
default rel

; ---------------------------------------------------------------------
; Constants
; ---------------------------------------------------------------------
section .rodata
    newline: db 0x0A

; Small stack buffer used by stdout_put_int. 21 bytes is enough for
; any signed 64-bit integer in decimal (-9223372036854775808..INT64_MAX).
section .bss
    intbuf: resb 24

; ---------------------------------------------------------------------
; Code
; ---------------------------------------------------------------------
section .text

; extern int_to_str(value:int64; buf:ptr) -> rax
extern int_to_str

; stdout_put_str: print a NUL-terminated string to stdout.
;   rdi = pointer to buffer
; Clobbers: rax, rcx, rdi, rsi, rdx
global stdout_put_str
stdout_put_str:
    ; Compute length by scanning for NUL byte.
    mov  rsi, rdi            ; rsi = start of string
    xor  rcx, rcx            ; rcx = length counter
.strlen_loop:
    mov  al, [rsi + rcx]
    test al, al
    jz   .strlen_done
    inc  rcx
    jmp  .strlen_loop
.strlen_done:
    ; sys_write(fd=1, buf=rsi, count=rcx)
    mov  rax, 1              ; sys_write
    mov  rdi, 1              ; stdout
    ; rsi already points to buf
    mov  rdx, rcx            ; length
    syscall
    ret

; stdout_put_nl: print a single newline character to stdout.
global stdout_put_nl
stdout_put_nl:
    mov  rax, 1              ; sys_write
    mov  rdi, 1              ; stdout
    lea  rsi, [rel newline]
    mov  rdx, 1
    syscall
    ret

; stdout_put_int: print a signed 64-bit integer in decimal to stdout.
;   rdi = integer value
; Clobbers: rax, rcx, rdx, rsi, rdi
global stdout_put_int
stdout_put_int:
    ; Convert value (rdi) into the static buffer intbuf.
    lea  rsi, [rel intbuf]
    call int_to_str
    ; rax now points to the start of the string in intbuf.
    ; Recurse into stdout_put_str to actually print it.
    mov  rdi, rax
    jmp  stdout_put_str
