# listfiles

Lists files from `fixtures/` and prints each file name plus its byte size.

This is the regression example that catches Win32 struct layout, `.dword` memory loads, `shl(32, reg)`, nested `if/while`, and multi-argument `stdout.put` issues.
