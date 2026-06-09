# strings (Windows)

Print ASCII printable runs of length >= 4 from `argv[1]` using `hlax_is_printable` and `hlax_stdout_write`.

```powershell
hla64 build examples/tools/10-windows/strings/strings.hla64 --target windows-x64-msabi -o build/win-strings
.\build\win-strings\strings.exe examples\tools\10-windows\strings\fixtures\sample-strings.txt
```
