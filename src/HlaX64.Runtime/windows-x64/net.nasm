; HlaX64 Runtime - TCP helpers (Windows x64 / MS ABI)
; DNS (IPv4), timeouts, reliable write. Requires ws2_32.lib.

bits 64
default rel

%define AF_INET         2
%define SOCK_STREAM     1
%define INVALID_SOCKET  0xFFFFFFFFFFFFFFFF
%define SOCKET_ERROR    0xFFFFFFFF
%define FIONBIO         0x8004667E
%define SOL_SOCKET      0xFFFF
%define SO_ERROR        0x1007
%define SO_RCVTIMEO     0x1006
%define SO_SNDTIMEO     0x1005
%define WSAEWOULDBLOCK  10035
%define WSAEINVAL       10022
%define WSAECONNREFUSED 10061
%define WSAHOST_NOT_FOUND 11001
%define AI_PASSIVE      1

extern WSAStartup
extern WSACleanup
extern WSAGetLastError
extern socket
extern connect
extern send
extern recv
extern closesocket
extern inet_addr
extern htons
extern ioctlsocket
extern select
extern getsockopt
extern setsockopt
extern getaddrinfo
extern freeaddrinfo
extern inet_ntoa
extern inet_ntop

section .bss
align 8
wsa_data:     resb 408
wsa_started:  resb 1
last_net_error: resd 1
dns_hints:    resb 48
dns_result:   resq 1
hostent_buf:  resq 1
ip_digits:    resb 16
timeout_ms:   resd 1
select_write: resb 520
select_time:  resq 2
so_error:     resd 1

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

hlax_net_init:
    push rbp
    mov rbp, rsp
    and rsp, -16
    cmp byte [wsa_started], 0
    jne .ok
    sub rsp, 48
    mov ecx, 514
    lea rdx, [rel wsa_data]
    call WSAStartup
    test eax, eax
    jnz .fail
    mov byte [wsa_started], 1
    add rsp, 48
.ok:
    xor rax, rax
    mov rsp, rbp
    pop rbp
    ret
.fail:
    add rsp, 48
    mov [last_net_error], eax
    mov rax, -1
    mov rsp, rbp
    pop rbp
    ret

hlax_net_last_error:
    mov eax, [last_net_error]
    movsxd rax, eax
    ret

; rcx=host, rdx=out_buf, r8=cap -> length or -1
hlax_dns_resolve_v4:
    push rbx
    push r12
    push r13
    push r14
    sub rsp, 48
    mov r12, rcx
    mov r13, rdx
    mov r14d, r8d

    mov rcx, r12
    sub rsp, 40
    call inet_addr
    add rsp, 40
    cmp eax, -1
    jne .from_binary

    ; localhost -> 127.0.0.1 for IPv4-only tests on modern Windows
    mov al, [r12]
    cmp al, 'l'
    jne .need_dns
    mov al, [r12 + 1]
    cmp al, 'o'
    jne .need_dns
    mov al, [r12 + 2]
    cmp al, 'c'
    jne .need_dns
    mov al, [r12 + 3]
    cmp al, 'a'
    jne .need_dns
    mov al, [r12 + 4]
    cmp al, 'l'
    jne .need_dns
    mov al, [r12 + 5]
    cmp al, 'h'
    jne .need_dns
    mov al, [r12 + 6]
    cmp al, 'o'
    jne .need_dns
    mov al, [r12 + 7]
    cmp al, 's'
    jne .need_dns
    mov al, [r12 + 8]
    cmp al, 't'
    jne .need_dns
    mov byte [r12 + 9], 0
    cmp byte [r12 + 9], 0
    jne .need_dns
    lea rsi, [rel localhost_ipv4]
    mov rdi, r13
    jmp .copy_cstr

.from_binary:
    mov rsi, r12
    mov rdi, r13
    jmp .copy_cstr

.need_dns:
    call hlax_net_init
    test rax, rax
    jnz .fail

    xor eax, eax
    mov rdi, dns_hints
    mov ecx, 48
.clear_hints:
    mov [rdi], al
    inc rdi
    loop .clear_hints
    mov dword [dns_hints + 4], AF_INET
    mov dword [dns_hints + 8], SOCK_STREAM

    mov qword [dns_result], 0
    mov rcx, r12
    xor rdx, rdx
    lea r8, [dns_hints]
    lea r9, [dns_result]
    sub rsp, 40
    call getaddrinfo
    add rsp, 40
    test eax, eax
    jnz .fail_gai

    mov rax, [dns_result]
    mov rcx, [rax + 32]
    test rcx, rcx
    jz .fail_free
    lea rdx, [rcx + 4]
    mov rcx, AF_INET
    mov r8, r13
    mov r9, r14
    sub rsp, 40
    call inet_ntop
    add rsp, 40
    test rax, rax
    jz .fail_free

    mov rdi, r13
    xor eax, eax
.len:
    cmp byte [rdi + rax], 0
    je .done_gai
    inc rax
    jmp .len
.done_gai:
    mov rbx, rax
    mov rcx, [dns_result]
    sub rsp, 40
    call freeaddrinfo
    add rsp, 40
    mov rax, rbx
    jmp .exit

.fail_free:
    mov rcx, [dns_result]
    test rcx, rcx
    jz .fail_inval
    sub rsp, 40
    call freeaddrinfo
    add rsp, 40
    jmp .fail_inval

.fail_gai:
    mov [last_net_error], eax
    jmp .fail

.fail_inval:
    mov dword [last_net_error], WSAEINVAL
.fail:
    mov rax, -1
    jmp .exit

.copy_cstr:
    mov al, [rsi]
    mov [rdi], al
    test al, al
    jz .done
    inc rsi
    inc rdi
    jmp .copy_cstr
.done:
    mov rax, rdi
    sub rax, r13
    dec rax

.exit:
    add rsp, 48
    pop r14
    pop r13
    pop r12
    pop rbx
    ret

hlax_internal_connect_ipv4:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    push r14
    push r15
    mov r13, rcx
    mov r12, rdx

    sub rsp, 8

    call hlax_net_init
    test rax, rax
    jnz connect_ipv4_fail

    sub rsp, 48
    mov ecx, AF_INET
    mov edx, SOCK_STREAM
    xor r8d, r8d
    call socket
    add rsp, 48
    cmp rax, INVALID_SOCKET
    je connect_ipv4_fail_socket
    mov ebx, eax

    mov rcx, r13
    sub rsp, 40
    call inet_addr
    add rsp, 40
    cmp eax, -1
    je connect_ipv4_inaddr_fail
    mov r13d, eax

    mov rcx, r12
    sub rsp, 40
    call htons
    mov r15w, ax
    add rsp, 40

    sub rsp, 48
    xor eax, eax
    mov [rsp+32], rax
    mov word [rsp+32], AF_INET
    mov [rsp + 34], r15w
    mov [rsp + 36], r13d

    mov rcx, rbx
    lea rdx, [rsp+32]
    mov r8d, 16
    call connect
    add rsp, 48
    test eax, eax
    jnz connect_ipv4_close_fail

    movsxd rax, ebx
    add rsp, 8
    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret

connect_ipv4_inaddr_fail:
    mov dword [last_net_error], WSAEINVAL
    jmp connect_ipv4_close_only

connect_ipv4_close_fail:
    sub rsp, 40
    call WSAGetLastError
    add rsp, 40
    test eax, eax
    jnz connect_ipv4_store_error
    mov eax, WSAECONNREFUSED
connect_ipv4_store_error:
    mov [last_net_error], eax

connect_ipv4_close_only:
    mov ecx, ebx
    sub rsp, 40
    call closesocket
    add rsp, 40
    mov rax, -1
    add rsp, 8
    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret

connect_ipv4_fail_socket:
    sub rsp, 40
    call WSAGetLastError
    mov [last_net_error], eax
    add rsp, 40
connect_ipv4_fail:
    mov rax, -1
    add rsp, 8
    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret

hlax_tcp_connect:
    jmp hlax_internal_connect_ipv4

; rcx=host, rdx=port
hlax_tcp_connect_name:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    mov r12, rcx
    mov r13, rdx
    sub rsp, 288
    mov rcx, r12
    lea rdx, [rsp+32]
    mov r8d, 256
    call hlax_dns_resolve_v4
    test rax, rax
    js .fail
    lea rcx, [rsp+32]
    mov rdx, r13
    call hlax_internal_connect_ipv4
    add rsp, 288
    pop r13
    pop r12
    pop rbx
    leave
    ret
.fail:
    add rsp, 288
    mov rax, -1
    pop r13
    pop r12
    pop rbx
    leave
    ret

; rcx=host, rdx=port, r8=timeout_ms
hlax_tcp_connect_timeout:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    push r14
    push r15
    mov r13, rcx
    mov r12, rdx
    mov r14d, r8d

    sub rsp, 48
    mov ecx, AF_INET
    mov edx, SOCK_STREAM
    xor r8d, r8d
    call socket
    add rsp, 48
    cmp rax, INVALID_SOCKET
    je .fail
    mov ebx, eax

    mov ecx, ebx
    mov edx, FIONBIO
    lea r8, [timeout_ms]
    mov dword [timeout_ms], 1
    call ioctlsocket

    mov rcx, r13
    sub rsp, 40
    call inet_addr
    add rsp, 40
    cmp eax, -1
    je .close_fail
    mov r13d, eax

    mov rcx, r12
    sub rsp, 40
    call htons
    mov r15w, ax
    add rsp, 40

    sub rsp, 48
    xor eax, eax
    mov [rsp+32], rax
    mov word [rsp+32], AF_INET
    mov [rsp + 34], r15w
    mov [rsp + 36], r13d

    mov rcx, rbx
    lea rdx, [rsp+32]
    mov r8d, 16
    call connect
    add rsp, 48
    test eax, eax
    jz .blocking_ok
    call WSAGetLastError
    cmp eax, WSAEWOULDBLOCK
    jne .close_fail

    mov dword [select_write], 1
    mov qword [select_write + 4], rbx
    mov rax, r14
    xor rdx, rdx
    mov rcx, 1000
    div rcx
    mov [select_time], rax
    mov rax, r14
    xor rdx, rdx
    mov rcx, 1000
    div rcx
    imul rdx, 1000
    mov [select_time + 8], rdx

    sub rsp, 48
    xor ecx, ecx
    xor edx, edx
    lea r8, [select_write]
    xor r9d, r9d
    lea rax, [select_time]
    mov [rsp+32], rax
    call select
    add rsp, 48
    test eax, eax
    jle .close_fail

    mov dword [so_error], 0
    sub rsp, 48
    mov ecx, ebx
    mov edx, SOL_SOCKET
    mov r8d, SO_ERROR
    lea r9, [so_error]
    mov qword [rsp+32], 4
    call getsockopt
    mov eax, [so_error]
    add rsp, 48
    test eax, eax
    jnz .close_fail

.blocking_ok:
    mov dword [timeout_ms], 0
    mov ecx, ebx
    mov edx, FIONBIO
    lea r8, [timeout_ms]
    call ioctlsocket
    movsxd rax, ebx
    jmp .done
.close_fail:
    mov ecx, ebx
    call closesocket
.fail:
    mov rax, -1
.done:
    pop r15
    pop r14
    pop r13
    pop r12
    pop rbx
    leave
    ret

; rcx=sock, rdx=recv_ms, r8=send_ms
hlax_tcp_set_timeouts_ms:
    push rbx
    sub rsp, 48
    mov ebx, ecx
    mov dword [rsp+32], edx
    mov dword [rsp+36], r8d
    mov ecx, ebx
    mov edx, SOL_SOCKET
    mov r8d, SO_RCVTIMEO
    lea r9, [rsp+32]
    call setsockopt
    test eax, eax
    jnz .fail
    mov ecx, ebx
    mov edx, SOL_SOCKET
    mov r8d, SO_SNDTIMEO
    lea r9, [rsp+36]
    call setsockopt
    test eax, eax
    jnz .fail
    xor eax, eax
    add rsp, 48
    pop rbx
    ret
.fail:
    mov rax, -1
    add rsp, 48
    pop rbx
    ret

hlax_tcp_write:
    mov r9d, 0
    jmp send

hlax_tcp_write_all:
    push rbp
    mov rbp, rsp
    push rbx
    push r12
    push r13
    push r14
    mov ebx, ecx
    mov r12, rdx
    mov r13, r8
    xor r14, r14
.loop:
    cmp r14, r13
    jge .done
    mov ecx, ebx
    mov rdx, r12
    add rdx, r14
    mov r8, r13
    sub r8, r14
    mov r9d, 0
    call send
    cmp rax, SOCKET_ERROR
    je .fail
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
    mov r9d, 0
    jmp recv

hlax_tcp_read_once:
    mov r9d, 0
    jmp recv

hlax_tcp_close:
    call closesocket
    cmp rax, SOCKET_ERROR
    je .fail
    xor eax, eax
    ret
.fail:
    mov rax, -1
    ret

section .data
localhost_ipv4 db "127.0.0.1", 0
