; HlaX64 Runtime - System helpers (Windows x64 / MS ABI)
; Wraps kernel32 and psapi helpers.

bits 64
default rel

%define MEMORYSTATUSEX_SIZE 64
%define FILE_SIZE_LOW_OFF   32
%define FILE_SIZE_HIGH_OFF  28
%define GetFileExInfoStandard 0
%define PROCESS_COUNTERS_SIZE 72

extern GetCurrentProcessId
extern GetComputerNameA
extern GetTickCount64
extern GlobalMemoryStatusEx
extern GetFileAttributesExA
extern GetLastError
extern GetSystemInfo
extern GetDiskFreeSpaceExA
extern GetCurrentProcess
extern GetProcessMemoryInfo

section .bss
align 8
mem_status:  resb 72
file_data:   resb 48
sys_info:    resb 64
proc_mem:    resb 80
disk_total:  resq 1
disk_avail:  resq 1

section .text

global hlax_getpid
global hlax_hostname
global hlax_uptime_secs
global hlax_mem_total
global hlax_mem_avail
global hlax_file_size
global hlax_os_last_error
global hlax_cpu_count
global hlax_disk_total_bytes
global hlax_disk_avail_bytes
global hlax_self_rss_bytes
global hlax_load_avg_milli

hlax_getpid:
    sub rsp, 40
    call GetCurrentProcessId
    mov eax, eax
    add rsp, 40
    ret

hlax_hostname:
    push rbx
    push rsi
    sub rsp, 48
    mov rbx, rcx
    mov dword [rsp+32], edx
    dec dword [rsp+32]
    lea rdx, [rsp+32]
    call GetComputerNameA
    test eax, eax
    jz .fail
    mov eax, [rsp+32]
    add rsp, 48
    pop rsi
    pop rbx
    ret
.fail:
    mov rax, -1
    add rsp, 48
    pop rsi
    pop rbx
    ret

hlax_uptime_secs:
    sub rsp, 40
    call GetTickCount64
    mov rcx, 1000
    xor edx, edx
    div rcx
    add rsp, 40
    ret

hlax_mem_total:
    push rbx
    sub rsp, 48
    mov dword [mem_status], MEMORYSTATUSEX_SIZE
    lea rcx, [mem_status]
    call GlobalMemoryStatusEx
    test eax, eax
    jz .fail
    mov rax, [mem_status + 8]
    add rsp, 48
    pop rbx
    ret
.fail:
    mov rax, -1
    add rsp, 48
    pop rbx
    ret

hlax_mem_avail:
    push rbx
    sub rsp, 48
    mov dword [mem_status], MEMORYSTATUSEX_SIZE
    lea rcx, [mem_status]
    call GlobalMemoryStatusEx
    test eax, eax
    jz .fail
    mov rax, [mem_status + 16]
    add rsp, 48
    pop rbx
    ret
.fail:
    mov rax, -1
    add rsp, 48
    pop rbx
    ret

hlax_file_size:
    push rbx
    sub rsp, 64
    mov r8, file_data
    mov edx, GetFileExInfoStandard
    call GetFileAttributesExA
    test eax, eax
    jz .fail
    mov eax, [file_data + FILE_SIZE_LOW_OFF]
    mov edx, [file_data + FILE_SIZE_HIGH_OFF]
    movsxd rcx, eax
    shl rdx, 32
    or rcx, rdx
    mov rax, rcx
    add rsp, 64
    pop rbx
    ret
.fail:
    mov rax, -1
    add rsp, 64
    pop rbx
    ret

hlax_os_last_error:
    sub rsp, 40
    call GetLastError
    mov eax, eax
    add rsp, 40
    ret

hlax_cpu_count:
    sub rsp, 48
    lea rcx, [sys_info]
    call GetSystemInfo
    mov eax, [sys_info + 32]
    movsxd rax, eax
    add rsp, 48
    ret

; rcx = path -> rax = total bytes or -1
hlax_disk_total_bytes:
    sub rsp, 56
    mov qword [rsp+40], 0
    lea r9, [disk_total]
    lea r8, [disk_total]
    lea rdx, [disk_avail]
    call GetDiskFreeSpaceExA
    test eax, eax
    jz .fail
    mov rax, [disk_total]
    add rsp, 56
    ret
.fail:
    mov rax, -1
    add rsp, 56
    ret

; rcx = path -> rax = available bytes or -1
hlax_disk_avail_bytes:
    sub rsp, 56
    mov qword [rsp+40], 0
    lea r9, [disk_total]
    lea r8, [disk_total]
    lea rdx, [disk_avail]
    call GetDiskFreeSpaceExA
    test eax, eax
    jz .fail
    mov rax, [disk_avail]
    add rsp, 56
    ret
.fail:
    mov rax, -1
    add rsp, 56
    ret

hlax_self_rss_bytes:
    sub rsp, 56
    mov dword [proc_mem], PROCESS_COUNTERS_SIZE
    call GetCurrentProcess
    mov rcx, rax
    lea rdx, [proc_mem]
    call GetProcessMemoryInfo
    test eax, eax
    jz .fail
    mov rax, [proc_mem + 16]
    add rsp, 56
    ret
.fail:
    mov rax, -1
    add rsp, 56
    ret

; unsupported on Windows
hlax_load_avg_milli:
    mov rax, -1
    ret
