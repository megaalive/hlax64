# tee (Windows)

Copy stdin bytes to stdout and `argv[1]` using `hlax_file_read(stdin_fd, ...)` plus `hlax_stdout_write` and `hlax_file_write`.

```powershell
hla64 build examples/tools/10-windows/tee/tee.hla64 --target windows-x64-msabi -o build/win-tee
"hello" | .\build\win-tee\tee.exe out.txt
```
