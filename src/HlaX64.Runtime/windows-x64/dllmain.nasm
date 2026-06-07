; windows-x64/dllmain.nasm — minimal PE DLL entry (no CRT).
bits 64
default rel

section .text
global DllMain

; BOOL WINAPI DllMain(HINSTANCE hinstDLL, DWORD fdwReason, LPVOID lpvReserved)
DllMain:
    mov eax, 1
    ret
