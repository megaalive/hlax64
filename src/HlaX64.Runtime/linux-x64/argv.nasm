; HlaX64 Runtime - argv stub (Linux x64)
; Full SysV argv parsing is planned; compile-only / link stubs return empty argv.

bits 64
default rel

section .text
global hlax_argv_init
global hlax_argv_count
global hlax_argv_get

hlax_argv_init:
    ret

hlax_argv_count:
    xor rax, rax
    ret

hlax_argv_get:
    xor rax, rax
    ret
