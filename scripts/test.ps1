# HlaX64 Test Script
# Usage: .\scripts\test.ps1

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== HlaX64 Test ===" -ForegroundColor Cyan
Write-Host ""

# Build and run tests
Write-Host "Running tests..." -ForegroundColor Yellow
dotnet test "$ProjectRoot\HlaX64.slnx" --nologo

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=== All tests passed ===" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "=== Tests failed ===" -ForegroundColor Red
    exit 1
}