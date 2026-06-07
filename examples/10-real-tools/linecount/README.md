# linecount

Reads `sample-b.txt` into a static buffer and counts newline bytes.

This example stresses `CreateFileA`, `ReadFile`, byte loads, pointer increments, and loop state over data read from the OS.

It builds and runs natively on Windows and prints `sample-b.txt lines: 2`. Getting this to execute uncovered and fixed two real compiler bugs: a Win64 stack-argument alignment bug (odd stack-arg counts such as `CreateFileA`'s 7 arguments produced a non-16-byte-aligned `sub rsp`) and a control-flow lowering bug where a `while` loop containing an `if` laid the loop continuation block after the `if` body, causing an infinite loop.
