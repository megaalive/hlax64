# Enable repo-local prepare-commit-msg hook that strips Cursor attribution.
# Usage: powershell -File scripts/setup-githooks.ps1
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot
git config --local core.hooksPath .githooks
Write-Host "core.hooksPath = .githooks (local only)"
Write-Host "Run: powershell -File scripts/audit-coauthors.ps1"
