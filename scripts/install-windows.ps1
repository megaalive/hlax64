# Bootstrap helper for Windows — checks toolchain and runs hla64 doctor.
# Full install paths: docs/install.md
param(
    [switch]$SkipDoctor
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
Set-Location $Root

Write-Host "HlaX64 bootstrap (Windows)"
Write-Host "Install guide: docs/install.md"
Write-Host ""

$missing = $false
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "Missing: .NET SDK — https://dotnet.microsoft.com/download/dotnet/10.0"
    $missing = $true
}
if (-not (Get-Command nasm -ErrorAction SilentlyContinue)) {
    Write-Host "Missing: NASM — winget install -e --id NASM.NASM"
    $missing = $true
}
if (-not (Get-Command lld-link -ErrorAction SilentlyContinue) -and -not (Get-Command link -ErrorAction SilentlyContinue)) {
    Write-Host "Missing: Windows linker — winget install -e --id LLVM.LLVM"
    $missing = $true
}

Write-Host ""
if ($missing) {
    Write-Host "Fix missing tools above, then re-run this script."
    exit 1
}

if ($SkipDoctor) { exit 0 }

dotnet run --project src/HlaX64.Cli -- doctor
exit $LASTEXITCODE
