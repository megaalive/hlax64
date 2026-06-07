# missing-return

Procedure `P` is declared with `@returns("rax")` but never assigns `rax` before `end P`. With verification warnings enabled, expect **HLAX0062**.

Also covered by `tests/conformance/invalid/missing-return/`.
