# HlaX64 Build Script
# Usage: .\scripts\build.ps1

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot

Write-Host "=== HlaX64 Build ===" -ForegroundColor Cyan
Write-Host ""

# Restore and build
Write-Host "Restoring packages..." -ForegroundColor Yellow
dotnet restore "$ProjectRoot\HlaX64.slnx" --nologo

Write-Host ""
Write-Host "Building solution..." -ForegroundColor Yellow
dotnet build "$ProjectRoot\HlaX64.slnx" --nologo --no-restore

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "=== Build succeeded ===" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "=== Build failed ===" -ForegroundColor Red
    exit 1
}