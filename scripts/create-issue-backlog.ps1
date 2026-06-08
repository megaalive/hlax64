# GitHub issue backlog for HlaX64
#
# Creates curated good-first and help-wanted issues via GitHub REST API.
# Requires git credential manager with github.com access (or GITHUB_TOKEN env).
#
# Usage:
#   pwsh -File scripts/create-issue-backlog.ps1
#   pwsh -File scripts/create-issue-backlog.ps1 -DryRun

param(
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"
$repo = "megaalive/hlax64"
$api = "https://api.github.com/repos/$repo"

function Get-GitHubToken {
    if ($env:GITHUB_TOKEN) { return $env:GITHUB_TOKEN }
    $input = "protocol=https`nhost=github.com`n"
    $cred = $input | git credential fill 2>$null
    foreach ($line in ($cred -split "`n")) {
        if ($line -match "^password=(.+)$") { return $Matches[1] }
    }
    throw "No GitHub token. Set GITHUB_TOKEN or sign in with git credential manager."
}

function Invoke-GhApi {
    param([string]$Method, [string]$Uri, [object]$Body = $null)
    $headers = @{
        Authorization = "Bearer $(Get-GitHubToken)"
        Accept        = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
    }
    if ($null -eq $Body) {
        return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers
    }
    $json = $Body | ConvertTo-Json -Depth 6 -Compress
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($json)
    return Invoke-RestMethod -Method $Method -Uri $Uri -Headers $headers -Body $bytes -ContentType "application/json; charset=utf-8"
}

function Ensure-Label {
    param([string]$Name, [string]$Color, [string]$Description)
    try {
        Invoke-GhApi GET "$api/labels/$([uri]::EscapeDataString($Name))"
    }
    catch {
        if ($DryRun) {
            Write-Host "[dry-run] create label: $Name"
            return
        }
        Invoke-GhApi POST "$api/labels" @{ name = $Name; color = $Color; description = $Description } | Out-Null
        Write-Host "Created label: $Name"
    }
}

function Get-ExistingIssueTitles {
    $titles = @()
    $page = 1
    while ($true) {
        $batch = Invoke-GhApi GET "$api/issues?state=all&per_page=100&page=$page"
        if (-not $batch -or $batch.Count -eq 0) { break }
        $titles += $batch | ForEach-Object { $_.title }
        if ($batch.Count -lt 100) { break }
        $page++
    }
    return $titles
}

function New-BacklogIssue {
    param(
        [string]$Title,
        [string]$Body,
        [string[]]$Labels
    )
    if ($script:ExistingTitles -contains $Title) {
        Write-Host "Skip (exists): $Title"
        return
    }
    if ($DryRun) {
        Write-Host "[dry-run] issue: $Title [$($Labels -join ', ')]"
        return
    }
    $issue = Invoke-GhApi POST "$api/issues" @{
        title  = $Title
        body   = $Body
        labels = $Labels
    }
    Write-Host "Created #$($issue.number): $Title"
    $script:ExistingTitles += $Title
}

$labels = @(
    @{ Name = "good first issue"; Color = "7057ff"; Description = "Good for newcomers" }
    @{ Name = "help wanted"; Color = "008672"; Description = "Community help appreciated" }
    @{ Name = "documentation"; Color = "0075ca"; Description = "Docs improvements" }
    @{ Name = "testing"; Color = "bfd4f2"; Description = "Tests and CI" }
    @{ Name = "tooling"; Color = "d4c5f9"; Description = "CLI, MCP, editor tooling" }
    @{ Name = "compiler"; Color = "e99695"; Description = "Compiler core" }
    @{ Name = "enhancement"; Color = "a2eeef"; Description = "New feature or improvement" }
)

foreach ($l in $labels) {
    Ensure-Label @l
}

$script:ExistingTitles = Get-ExistingIssueTitles

$issues = @(
    @{
        Title = "docs: add worked examples for each HLAX diagnostic code"
        Labels = @("good first issue", "documentation")
        Body = @"
## Summary
Expand ``docs/diagnostics.md`` with a short example snippet and fix suggestion for every published ``HLAX####`` code.

## Files
- ``docs/diagnostics.md``
- ``src/HlaX64.Compiler/`` (reference for message text)

## Acceptance criteria
- [ ] Each documented code has a minimal failing ``.hla64`` example or test reference
- [ ] Cross-link from README diagnostics section
- [ ] ``dotnet test`` unchanged

## How to test

Run dotnet test from repo root.
"@
    }
    @{
        Title = "test: add uint64 boundary comparison integration case"
        Labels = @("good first issue", "testing")
        Body = @"
## Summary
Add a native manifest covering unsigned compare at ``0x8000000000000000`` vs ``1`` (high-bit edge case).

## Files
- ``tests/samples/`` or ``tests/examples-curriculum/``
- Optional source under ``examples/curriculum/02-types/``

## Acceptance criteria
- [ ] Manifest runs in ``hla64 test`` on Linux CI
- [ ] Expected stdout/exit documented in manifest JSON

## How to test

    dotnet run --project src/HlaX64.Cli -- test tests/samples --filter unsigned
"@
    }
    @{
        Title = "tooling: add scripts/install-linux.sh bootstrap script"
        Labels = @("good first issue", "tooling", "documentation")
        Body = @"
## Summary
Shell script that checks ``dotnet``, ``nasm``, ``gcc``, clones optional, and prints PATH hints. Link from ``docs/install.md``.

## Acceptance criteria
- [ ] Idempotent; no sudo required beyond apt suggestion text
- [ ] Documented in install guide

## How to test
Run on Ubuntu/WSL and verify ``hla64 doctor`` guidance is clear.
"@
    }
    @{
        Title = "tooling: add scripts/install-windows.ps1 helper"
        Labels = @("good first issue", "tooling")
        Body = @"
## Summary
PowerShell helper: verify .NET 10 SDK, suggest ``choco install nasm`` / LLVM link, optional global tool install.

## Acceptance criteria
- [ ] Linked from ``docs/install.md``
- [ ] Exits non-zero when NASM missing (with fix instructions)
"@
    }
    @{
        Title = "tooling: improve Doctor output when NASM is missing"
        Labels = @("good first issue", "tooling")
        Body = @"
## Summary
When NASM is not found, ``hla64 doctor`` should link to ``docs/install.md`` and show OS-specific install commands.

## Files
- ``src/HlaX64.Cli/Services/DoctorService.cs`` (or shared ``DoctorReport``)

## Acceptance criteria
- [ ] Doctor JSON includes ``remediation`` field or richer ``detail`` text
- [ ] Unit test for message content
"@
    }
    @{
        Title = "docs: wire VS Code extension to Language Server in README"
        Labels = @("good first issue", "documentation", "tooling")
        Body = @"
## Summary
Update ``editors/vscode/README.md`` with a copy-paste ``settings.json`` block launching ``HlaX64.LanguageServer``.

## Acceptance criteria
- [ ] Steps verified on VS Code / Cursor
- [ ] Link from main README editor section
"@
    }
    @{
        Title = "docs: add CONTRIBUTING walkthrough with sample PR"
        Labels = @("good first issue", "documentation")
        Body = @"
## Summary
Add docs/contributing-walkthrough.md: fork, branch, test, PR template example for a docs-only change.

## Acceptance criteria
- [ ] Linked from CONTRIBUTING.md
- [ ] Notes optional githooks setup
"@
    }
    @{
        Title = "test: NASM snapshot for hello example (conformance)"
        Labels = @("good first issue", "testing")
        Body = @"
## Summary
Add conformance case with ``expectNasmContains`` substrings for ``examples/curriculum/00-getting-started/hello.hla64``.

## Files
- ``tests/conformance/valid/hello-snapshot/``

## Acceptance criteria
- [ ] CI runs via ``ConformanceTests``
- [ ] Substrings stable across platforms (avoid absolute paths)
"@
    }
    @{
        Title = "docs: expand examples/benchmarks README with bench workflow"
        Labels = @("good first issue", "documentation")
        Body = @"
## Summary
Document ``hla64 bench`` using existing ``benchmarks/count.json`` manifest.

## Acceptance criteria
- [ ] Shows JSON output sample
- [ ] Cross-link ``schemas/bench-result.schema.json``
"@
    }
    @{
        Title = "chore: add .editorconfig entries for .hla64 files"
        Labels = @("good first issue", "tooling")
        Body = @"
## Summary
Add ``[*.hla64]`` section: indent, charset, final newline.

## Acceptance criteria
- [ ] Does not reformat entire examples tree in same PR (config only)
"@
    }
    @{
        Title = "example: Rust FFI sample calling exported HlaX64 library"
        Labels = @("help wanted", "enhancement")
        Body = @"
## Summary
Add ``examples/interop/07-interop/rust-ffi/`` showing ``libloading`` or ``extern ""C""`` against ``export-lib`` output.

## Acceptance criteria
- [ ] README with build steps on Linux
- [ ] Optional CI compile-only job
"@
    }
    @{
        Title = "RFC: DWARF debug info emission design"
        Labels = @("help wanted", "compiler", "documentation")
        Body = @"
## Summary
Author ``rfcs/0003-dwarf-debug-info.md``: scope for ``--emit-debug``, NASM ``dwarf`` directives, toolchain requirements.

## Acceptance criteria
- [ ] Alternatives considered (external assembler flags vs compiler metadata)
- [ ] Non-goals for v0.2 listed
"@
    }
    @{
        Title = "playground: live explain API backend for web demo"
        Labels = @("help wanted", "tooling", "enhancement")
        Body = @"
## Summary
Minimal ASP.NET or static WASM backend exposing read-only ``explain`` for curated examples. See ``docs/playground-design.md``.

## Acceptance criteria
- [ ] Security: path allowlist, no arbitrary ``run``
- [ ] Deploy story documented (GitHub Pages + separate host)
"@
    }
    @{
        Title = "LSP: hover and completion MVP for .hla64"
        Labels = @("help wanted", "tooling", "compiler")
        Body = @"
## Summary
Extend ``HlaX64.LanguageServer`` beyond diagnostics: hover for mnemonics, keyword completion.

## Acceptance criteria
- [ ] Works over stdio with VS Code client
- [ ] Tests for handler responses
"@
    }
    @{
        Title = "CI: run curriculum native tests on Windows"
        Labels = @("help wanted", "testing")
        Body = @"
## Summary
Windows job: ``hla64 test tests/examples-curriculum`` with MS ABI + lld-link where available.

## Files
- ``.github/workflows/ci.yml``

## Acceptance criteria
- [ ] At least hello + exitcode run on ``windows-latest``
- [ ] Graceful skip if linker missing (documented)
"@
    }
    @{
        Title = "tooling: benchmark report exporter (HTML or CSV)"
        Labels = @("help wanted", "tooling")
        Body = @"
## Summary
``hla64 bench --report out.html`` from JSON manifest; useful for regression tracking.

## Acceptance criteria
- [ ] Schema version preserved in output
- [ ] Unit test with golden fragment
"@
    }
    @{
        Title = "security: audit MCP sandbox path restrictions"
        Labels = @("help wanted", "tooling")
        Body = @"
## Summary
Review ``HlaX64.McpServer`` tools for path traversal, symlink escapes, and ``run`` timeouts. Document in ``docs/mcp-security.md``.

## Acceptance criteria
- [ ] Checklist in mcp-security.md updated
- [ ] Tests for rejected paths outside workspace
"@
    }
    @{
        Title = "compiler: warn on user register vs runtime clobber conflicts"
        Labels = @("help wanted", "compiler")
        Body = @"
## Summary
Use ``docs/runtime-contract.md`` metadata to emit HLAX warnings when user holds values in clobbered registers across runtime calls.

## Acceptance criteria
- [ ] Diagnostic with source location
- [ ] Conformance invalid case
"@
    }
    @{
        Title = "formatter: AST pretty-print edge cases (nested if/while)"
        Labels = @("help wanted", "compiler", "tooling")
        Body = @"
## Summary
Extend AstFormatter coverage; add conformance round-trip tests (parse, format, parse).

## Files
- ``src/HlaX64.Compiler/Formatting/AstFormatter.cs``
- ``tests/conformance/``
"@
    }
    @{
        Title = "community: seed GitHub Discussions FAQ threads"
        Labels = @("help wanted", "documentation")
        Body = @"
## Summary
After enabling Discussions, create pinned FAQ: install, Linux vs Windows target, MCP setup, contributing first PR.

## Acceptance criteria
- [ ] 4–6 starter threads with links to docs
- [ ] Linked from README community section
"@
    }
)

foreach ($item in $issues) {
    New-BacklogIssue -Title $item.Title -Body $item.Body -Labels $item.Labels
}

Write-Host "Done. Total issues in backlog script: $($issues.Count)"
