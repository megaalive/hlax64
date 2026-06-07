# Rewrite branch history to remove Cursor co-author / Made-with trailers.
# Usage: powershell -File scripts/strip-coauthor-history.ps1 [-Branch main] [-Force]
param(
    [string]$Branch = "main",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

& "$PSScriptRoot/audit-coauthors.ps1" -Branch $Branch
if ($LASTEXITCODE -eq 0) {
    Write-Host "Nothing to rewrite on '$Branch'."
    exit 0
}

if (-not $Force) {
    Write-Host ""
    Write-Host "This rewrites commit messages on '$Branch' (git filter-branch)."
    Write-Host "Re-run with -Force to proceed, then: git push --force-with-lease origin $Branch"
    exit 2
}

$env:FILTER_BRANCH_SQUELCH_WARNING = "1"
git filter-branch -f --msg-filter "powershell -NoProfile -ExecutionPolicy Bypass -File `"$PSScriptRoot/filter-commit-msg.ps1`"" $Branch
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

git for-each-ref --format="delete %(refname)" refs/original/ | git update-ref --stdin 2>$null
git reflog expire --expire=now --all
git gc --prune=now --quiet

& "$PSScriptRoot/audit-coauthors.ps1" -Branch $Branch
exit $LASTEXITCODE
