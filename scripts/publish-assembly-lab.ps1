# Publish HlaX64 Assembly Lab for one or more RIDs.
# Usage:
#   .\scripts\publish-assembly-lab.ps1
#   .\scripts\publish-assembly-lab.ps1 -Rids win-x64,linux-x64 -Configuration Release

param(
    [string[]]$Rids = @("win-x64", "linux-x64"),
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish/assembly-lab"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot "src/HlaX64.AssemblyLab/HlaX64.AssemblyLab.csproj"

Write-Host "=== Publish Assembly Lab ($Configuration) ===" -ForegroundColor Cyan

foreach ($rid in $Rids) {
    $outDir = Join-Path $ProjectRoot (Join-Path $OutputRoot $rid)
    Write-Host "Publishing $rid -> $outDir" -ForegroundColor Yellow
    dotnet publish $Project -c $Configuration -r $rid --self-contained false -o $outDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Copy-Item (Join-Path $ProjectRoot "LICENSE") $outDir -Force
    Copy-Item (Join-Path $ProjectRoot "README.md") $outDir -Force
}

Write-Host ""
Write-Host "=== Assembly Lab publish complete ===" -ForegroundColor Green
