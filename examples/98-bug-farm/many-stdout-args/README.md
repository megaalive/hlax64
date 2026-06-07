# many-stdout-args

Ten string literals plus `nl` in one `stdout.put` call. Stresses runtime call expansion and Win64/SysV save/restore around repeated `stdout_put_*` calls.

Expected exit code: **0** (compile-only curriculum manifest).
