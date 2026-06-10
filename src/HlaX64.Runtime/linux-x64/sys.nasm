; HlaX64 Runtime - System helpers (Linux x64 / System V ABI)
; Wraps libc/kernel helpers. Link with -lc when used.

bits 64
default rel

extern getpid
extern gethostname
extern sysinfo
extern stat
extern __errno_location
extern sysconf
extern statvfs
extern getrusage

%define STAT_SIZE_OFF       48
%define SYSINFO_UPTIME_OFF  0
%define SYSINFO_TOTAL_OFF   32
%define SYSINFO_FREE_OFF    40
%define SYSINFO_LOAD_OFF    8
%define SC_NPROCESSORS_ONLN 84
%define RUSAGE_SELF         0
%define RUSAGE_MAXRSS_OFF   32
%define STATVFS_F_BSIZE     0
%define STATVFS_F_BAVAIL    32
%define STATVFS_F_BLOCKS    16

section .bss
align 8
sysinfo_buf: resb 112
stat_buf:    resb 144
statvfs_buf: resb 120
rusage_buf:  resb 144

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

; HLAX64-RUNTIME-FUNCTION v0.1
; name: hlax_getpid
; target: linux-x64-sysv
; returns: rax = process id

hlax_getpid:
    call getpid
    ret

; rdi = buffer, rsi = capacity -> rax = hostname length or -1
hlax_hostname:
    push rbx
    mov rbx, rdi
    call gethostname
    test eax, eax
    jnz .fail
    mov rdi, rbx
    xor eax, eax
.len:
    cmp byte [rdi + rax], 0
    je .done
    inc rax
    jmp .len
.done:
    pop rbx
    ret
.fail:
    mov rax, -1
    pop rbx
    ret

hlax_uptime_secs:
    mov rdi, sysinfo_buf
    call sysinfo
    test eax, eax
    js .fail
    mov rax, [sysinfo_buf + SYSINFO_UPTIME_OFF]
    ret
.fail:
    mov rax, -1
    ret

hlax_mem_total:
    mov rdi, sysinfo_buf
    call sysinfo
    test eax, eax
    js .fail
    mov rax, [sysinfo_buf + SYSINFO_TOTAL_OFF]
    ret
.fail:
    mov rax, -1
    ret

hlax_mem_avail:
    mov rdi, sysinfo_buf
    call sysinfo
    test eax, eax
    js .fail
    mov rax, [sysinfo_buf + SYSINFO_FREE_OFF]
    ret
.fail:
    mov rax, -1
    ret

hlax_file_size:
    push rbx
    mov rsi, stat_buf
    call stat
    pop rbx
    test eax, eax
    js .fail
    mov rax, [stat_buf + STAT_SIZE_OFF]
    ret
.fail:
    mov rax, -1
    ret

; HLAX64-RUNTIME-FUNCTION v0.1
; name: hlax_os_last_error
; target: linux-x64-sysv
; returns: rax = errno

; -> rax = errno
hlax_os_last_error:
    call __errno_location
    mov rax, [rax]
    ret

; -> rax = online processor count or -1
hlax_cpu_count:
    mov rdi, SC_NPROCESSORS_ONLN
    call sysconf
    cmp rax, 0
    jl .fail
    ret
.fail:
    mov rax, -1
    ret

; rdi = path -> rax = total bytes or -1
hlax_disk_total_bytes:
    push rbx
    mov rsi, statvfs_buf
    call statvfs
    pop rbx
    test eax, eax
    js .fail
    mov rax, [statvfs_buf + STATVFS_F_BLOCKS]
    mov rcx, [statvfs_buf + STATVFS_F_BSIZE]
    xor rdx, rdx
    mul rcx
    ret
.fail:
    mov rax, -1
    ret

; rdi = path -> rax = available bytes or -1
hlax_disk_avail_bytes:
    push rbx
    mov rsi, statvfs_buf
    call statvfs
    pop rbx
    test eax, eax
    js .fail
    mov rax, [statvfs_buf + STATVFS_F_BAVAIL]
    mov rcx, [statvfs_buf + STATVFS_F_BSIZE]
    xor rdx, rdx
    mul rcx
    ret
.fail:
    mov rax, -1
    ret

; -> rax = resident set bytes (self) or -1
hlax_self_rss_bytes:
    push rbx
    mov rdi, RUSAGE_SELF
    mov rsi, rusage_buf
    xor rdx, rdx
    call getrusage
    pop rbx
    test eax, eax
    js .fail
    mov rax, [rusage_buf + RUSAGE_MAXRSS_OFF]
    imul rax, 1024
    ret
.fail:
    mov rax, -1
    ret

; -> rax = 1-minute load average in milli-units or -1
hlax_load_avg_milli:
    mov rdi, sysinfo_buf
    call sysinfo
    test eax, eax
    js .fail
    mov rax, [sysinfo_buf + SYSINFO_LOAD_OFF]
    imul rax, 1000
    mov rcx, 65536
    xor rdx, rdx
    div rcx
    ret
.fail:
    mov rax, -1
    ret
