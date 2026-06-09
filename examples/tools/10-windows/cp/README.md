# cp (Windows)

Copy `argv[1]` bytes to `argv[2]` using `hlax_file_open_write` and a read/write loop.

```powershell
hla64 build examples/tools/10-windows/cp/cp.hla64 --target windows-x64-msabi -o build/win-cp
.\build\win-cp\cp.exe src.txt dst.txt
```
