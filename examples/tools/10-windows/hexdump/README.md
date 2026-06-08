# hexdump

Prints `00000000` plus space-separated hex bytes for the file passed as `argv[1]`.

## Stress

Argv runtime, CreateFileA/ReadFile, pointer end-sentinel loop, hex nibble formatting via branch tree, nested calls inside loop (callee-saved cursor in `r14`, end in `r13`).

## Usage

```
hexdump.exe <file>
```

## Run

From repo root after building for `windows-x64-msabi`:

```powershell
hla64 build examples/tools/10-windows/hexdump/hexdump.hla64 --target windows-x64-msabi -o build/real-hexdump
build\real-hexdump\hexdump.exe examples\10-real-tools\hexdump\fixtures\sample-a.txt
```

## Expected

```
00000000  61 6c 70 68 61 0a 
```

Exit code `0`.
