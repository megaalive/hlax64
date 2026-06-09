# cat (Windows)

Copy `argv[1]` bytes to stdout using `hlax_file_*` read loop and `hlax_stdout_write`.

```powershell
hla64 build examples/tools/10-windows/cat/cat.hla64 --target windows-x64-msabi -o build/win-cat
.\build\win-cat\cat.exe examples\tools\10-windows\cat\fixtures\sample-cat.txt
```
