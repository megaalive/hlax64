# register-pressure

Keeps `r12`–`r14` live across repeated `call Step` inside a `while` loop. Mirrors the callee-saved cursor pattern used by real tools (`hexdump`, `listfiles`).

Expected exit code: **0** (compile-only curriculum manifest).
