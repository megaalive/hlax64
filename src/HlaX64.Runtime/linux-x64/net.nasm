; HlaX64 Runtime - TCP helpers (Linux x64 / System V ABI)
; DNS (IPv4), timeouts, reliable write. Link with -lc when used.

bits 64
default rel

extern socket
extern connect
extern send
extern recv
extern close
extern inet_addr
extern htons
extern __errno_location
extern getaddrinfo
extern freeaddrinfo
extern inet_ntop
extern fcntl
extern poll
extern getsockopt
extern setsockopt

%define AF_INET         2
%define SOCK_STREAM     1
%define F_GETFL         3
%define F_SETFL         4
%define O_NONBLOCK      2048
%define SOL_SOCKET      1
%define SO_ERROR        4
%define SO_RCVTIMEO     20
%define SO_SNDTIMEO     21
%define POLL_OUT        4
%define EINPROGRESS     115
%define AI_PASSIVE      1

section .bss
align 8
dns_hints:   resb 48
dns_result:  resq 1
timeval_buf: resb 16

section .text

global hlax_net_init
global hlax_net_last_error
global hlax_dns_resolve_v4
global hlax_tcp_connect
global hlax_tcp_connect_name
global hlax_tcp_connect_timeout
global hlax_tcp_set_timeouts_ms
global hlax_tcp_write
global hlax_tcp_write_all
global hlax_tcp_read
global hlax_tcp_read_once
global hlax_tcp_close

; HLAX64-RUNTIME-FUNCTION v0.1
; name: hlax_net_init
; target: linux-x64-sysv
; returns: rax = 0

hlax_net_init:
    xor rax, rax
    ret

hlax_net_last_error:
    call __errno_location
    mov rax, [rax]
    ret

; rdi=host, rsi=out_buf, rdx=cap -> length or -1
hlax_dns_resolve_v4:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    mov r12, rdi
    mov r13, rsi
    mov rbx, rdx

    xor eax, eax
    mov rdi, dns_hints
    mov ecx, 48
.clear:
    mov [rdi], al
    inc rdi
    loop .clear
    mov dword [dns_hints + 4], AF_INET
    mov dword [dns_hints + 8], SOCK_STREAM

    mov rdi, r12
    xor esi, esi
    lea rdx, [dns_hints]
    lea rcx, [dns_result]
    call getaddrinfo
    test eax, eax
    jnz .fail

    mov rax, [dns_result]
    mov rsi, [rax + 24]
    add rsi, 4
    mov edi, AF_INET
    mov rdx, r13
    mov rcx, rbx
    call inet_ntop
    test rax, rax
    jz .free_fail
    mov rdi, r13
    xor eax, eax
.len:
    cmp byte [rdi + rax], 0
    je .done
    inc rax
    jmp .len
.done:
    mov rdi, [dns_result]
    call freeaddrinfo
    pop r13
    pop r12
    pop rbx
    leave
    ret
.free_fail:
    mov rdi, [dns_result]
    call freeaddrinfo
.fail:
    mov rax, -1
    pop r13
    pop r12
    pop rbx
    leave
    ret

hlax_internal_connect_ipv4:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    mov r13, rdi
    mov r12, rsi

    mov edi, AF_INET
    mov esi, SOCK_STREAM
    xor edx, edx
    call socket
    cmp eax, 0
    jl .fail
    mov ebx, eax

    mov rdi, r13
    call inet_addr
    cmp eax, -1
    je .close_fail
    mov r13d, eax

    sub rsp, 16
    mov word [rsp], AF_INET
    mov rdi, r12
    call htons
    mov [rsp + 2], ax
    mov [rsp + 4], r13d

    mov edi, ebx
    mov rsi, rsp
    mov edx, 16
    call connect
    add rsp, 16
    test eax, eax
    js .close_fail

    mov eax, ebx
    pop r13
    pop r12
    pop rbx
    leave
    ret
.close_fail:
    mov edi, ebx
    call close
.fail:
    mov rax, -1
    pop r13
    pop r12
    pop rbx
    leave
    ret

hlax_tcp_connect:
    jmp hlax_internal_connect_ipv4

; rdi=host, rsi=port
hlax_tcp_connect_name:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    mov r12, rdi
    mov r13, rsi
    sub rsp, 272
    mov rdi, r12
    lea rsi, [rsp+16]
    mov rdx, 256
    call hlax_dns_resolve_v4
    test rax, rax
    js .fail
    lea rdi, [rsp+16]
    mov rsi, r13
    call hlax_internal_connect_ipv4
    add rsp, 272
    pop r13
    pop r12
    pop rbx
    leave
    ret
.fail:
    add rsp, 272
    mov rax, -1
    pop r13
    pop r12
    pop rbx
    leave
    ret

; rdi=host, rsi=port, rdx=timeout_ms
hlax_tcp_connect_timeout:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    push r14
    mov r13, rdi
    mov r12, rsi
    mov r14, rdx

    mov edi, AF_INET
    mov esi, SOCK_STREAM
    xor edx, edx
    call socket
    cmp eax, 0
    jl .fail
    mov ebx, eax

    mov edi, ebx
    mov esi, F_GETFL
    xor edx, edx
    call fcntl
    mov edx, eax
    or edx, O_NONBLOCK
    mov edi, ebx
    mov esi, F_SETFL
    call fcntl

    mov rdi, r13
    call inet_addr
    cmp eax, -1
    je .close_fail
    mov r13d, eax
    sub rsp, 32
    mov word [rsp], AF_INET
    mov rdi, r12
    call htons
    mov [rsp + 2], ax
    mov [rsp + 4], r13d
    mov edi, ebx
    mov rsi, rsp
    mov edx, 16
    call connect
    add rsp, 16
    test eax, eax
    jz .blocking_ok
    call __errno_location
    mov eax, [rax]
    cmp eax, EINPROGRESS
    jne .close_fail

    sub rsp, 16
    mov dword [rsp], ebx
    mov word [rsp + 4], POLL_OUT
    xor edi, edi
    mov rsi, rsp
    mov rdx, 1
    mov rcx, r14
    call poll
    add rsp, 16
    test eax, eax
    jle .close_fail

    sub rsp, 16
    mov dword [rsp + 8], 4
    lea r8, [rsp + 8]
    mov edi, ebx
    mov esi, SOL_SOCKET
    mov edx, SO_ERROR
    lea rcx, [rsp]
    call getsockopt
    mov eax, dword [rsp]
    add rsp, 16
    test eax, eax
    jnz .close_fail

.blocking_ok:
    mov edi, ebx
    mov esi, F_GETFL
    xor edx, edx
    call fcntl
    mov edx, eax
    and edx, ~O_NONBLOCK
    mov edi, ebx
    mov esi, F_SETFL
    call fcntl
    mov eax, ebx
    jmp .done
.close_fail:
    mov edi, ebx
    call close
.fail:
    mov rax, -1
.done:
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret

; rdi=sock, rsi=recv_ms, rdx=send_ms
hlax_tcp_set_timeouts_ms:
    push rbp
    mov rbp, rsp
    push rbx
    mov ebx, edi
    mov r12, rsi
    mov r13, rdx

    mov qword [timeval_buf], 0
    mov qword [timeval_buf + 8], 0
    mov rax, r12
    xor rdx, rdx
    mov rcx, 1000
    div rcx
    mov [timeval_buf], rax
    imul rdx, rdx, 1000
    mov [timeval_buf + 8], rdx

    mov edi, ebx
    mov esi, SOL_SOCKET
    mov edx, SO_RCVTIMEO
    lea rcx, [timeval_buf]
    mov r8d, 16
    call setsockopt
    test eax, eax
    js .fail

    mov qword [timeval_buf], 0
    mov qword [timeval_buf + 8], 0
    mov rax, r13
    xor rdx, rdx
    mov rcx, 1000
    div rcx
    mov [timeval_buf], rax
    imul rdx, rdx, 1000
    mov [timeval_buf + 8], rdx

    mov edi, ebx
    mov esi, SOL_SOCKET
    mov edx, SO_SNDTIMEO
    lea rcx, [timeval_buf]
    mov r8d, 16
    call setsockopt
    test eax, eax
    js .fail
    xor eax, eax
    pop rbx
    leave
    ret
.fail:
    mov rax, -1
    pop rbx
    leave
    ret

hlax_tcp_write:
    xor ecx, ecx
    jmp send

; rdi=sock, rsi=buf, rdx=count
hlax_tcp_write_all:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    push r14
    mov ebx, edi
    mov r12, rsi
    mov r13, rdx
    xor r14, r14
.loop:
    cmp r14, r13
    jge .done
    mov edi, ebx
    mov rsi, r12
    add rsi, r14
    mov rdx, r13
    sub rdx, r14
    xor ecx, ecx
    call send
    test rax, rax
    js .fail
    cmp rax, 0
    je .fail
    add r14, rax
    jmp .loop
.done:
    mov rax, r13
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret
.fail:
    mov rax, -1
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret

hlax_tcp_read:
    xor ecx, ecx
    jmp recv

hlax_tcp_read_once:
    xor ecx, ecx
    jmp recv

; rdi=sock -> 0 or -1
hlax_tcp_close:
    call close
    test eax, eax
    js .fail
    xor eax, eax
    ret
.fail:
    mov rax, -1
    ret
