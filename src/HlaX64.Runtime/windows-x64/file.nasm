; HlaX64 Runtime - File helpers (Windows x64 / MS ABI)
; Wraps GetFileAttributesA, CreateFileA, ReadFile, CloseHandle (kernel32).

bits 64
default rel

%define GENERIC_READ            0x80000000
%define GENERIC_WRITE           0x40000000
%define FILE_SHARE_READ         1
%define OPEN_EXISTING           3
%define CREATE_ALWAYS           2
%define FILE_ATTRIBUTE_NORMAL   0x80
%define INVALID_FILE_ATTRIBUTES 0xFFFFFFFF
%define STD_OUTPUT_HANDLE       -11

extern GetFileAttributesA
extern CreateFileA
extern ReadFile
extern WriteFile
extern CloseHandle
extern GetStdHandle

section .text

global hlax_path_exists
global hlax_file_open_read
global hlax_file_open_write
global hlax_file_read
global hlax_file_write
global hlax_file_close
global hlax_stdout_write

; rcx = path -> rax = 1 exists, 0 missing
hlax_path_exists:
    sub rsp, 40
    call GetFileAttributesA
    cmp eax, INVALID_FILE_ATTRIBUTES
    je .missing
    mov rax, 1
    add rsp, 40
    ret
.missing:
    xor rax, rax
    add rsp, 40
    ret

; rcx = path -> rax = handle or -1
hlax_file_open_read:
    sub rsp, 64
    mov r8d, FILE_SHARE_READ
    mov edx, GENERIC_READ
    xor r9d, r9d
    mov qword [rsp+32], OPEN_EXISTING
    mov qword [rsp+40], FILE_ATTRIBUTE_NORMAL
    mov qword [rsp+48], 0
    call CreateFileA
    add rsp, 64
    ret

; rcx = handle, rdx = buffer, r8 = count -> rax = bytes read or -1
hlax_file_read:
    sub rsp, 48
    mov qword [rsp+40], 0
    lea r9, [rsp+40]
    mov qword [rsp+32], 0
    call ReadFile
    test eax, eax
    jz .fail
    mov rax, [rsp+40]
    add rsp, 48
    ret
.fail:
    mov rax, -1
    add rsp, 48
    ret

; rcx = path -> rax = handle or -1
hlax_file_open_write:
    sub rsp, 64
    mov r8d, 0
    mov edx, GENERIC_WRITE
    xor r9d, r9d
    mov qword [rsp+32], CREATE_ALWAYS
    mov qword [rsp+40], FILE_ATTRIBUTE_NORMAL
    mov qword [rsp+48], 0
    call CreateFileA
    add rsp, 64
    ret

; rcx = handle, rdx = buffer, r8 = count -> rax = bytes written or -1
hlax_file_write:
    sub rsp, 48
    mov qword [rsp+40], 0
    lea r9, [rsp+40]
    mov qword [rsp+32], 0
    call WriteFile
    test eax, eax
    jz .fail
    mov rax, [rsp+40]
    add rsp, 48
    ret
.fail:
    mov rax, -1
    add rsp, 48
    ret

; rcx = buffer, rdx = count -> rax = bytes written to stdout or -1
hlax_stdout_write:
    push rbx
    push rsi
    push rdi
    mov rsi, rcx
    mov rdi, rdx
    mov ecx, STD_OUTPUT_HANDLE
    call GetStdHandle
    mov rcx, rax
    mov rdx, rsi
    mov r8, rdi
    sub rsp, 48
    mov qword [rsp+40], 0
    lea r9, [rsp+40]
    mov qword [rsp+32], 0
    call WriteFile
    test eax, eax
    jz .fail
    mov rax, [rsp+40]
    add rsp, 48
    pop rdi
    pop rsi
    pop rbx
    ret
.fail:
    mov rax, -1
    add rsp, 48
    pop rdi
    pop rsi
    pop rbx
    ret

; rcx = handle -> rax = 0
hlax_file_close:
    sub rsp, 40
    call CloseHandle
    xor eax, eax
    add rsp, 40
    ret
