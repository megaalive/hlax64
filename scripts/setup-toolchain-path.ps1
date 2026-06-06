# Add HlaX64 toolchain directories to the Windows User PATH.
# Run: powershell -ExecutionPolicy Bypass -File scripts/setup-toolchain-path.ps1
#
# NASM (required): install via choco install nasm -y
# Linux ELF link+run on Windows: WSL Ubuntu + gcc (MinGW cannot link elf64 objects)
#   wsl --install -d Ubuntu
#   wsl -d Ubuntu sudo apt install -y gcc nasm

param(
    [switch]$Machine
)

$ErrorActionPreference = "Stop"

$candidates = @(
    "$env:LOCALAPPDATA\bin\NASM"
    "C:\Program Files\NASM"
    "C:\ProgramData\mingw64\mingw64\bin"
)

$existing = if ($Machine) {
    [Environment]::GetEnvironmentVariable("Path", "Machine")
} else {
    [Environment]::GetEnvironmentVariable("Path", "User")
}

$added = @()
foreach ($dir in $candidates) {
    if (-not (Test-Path $dir)) { continue }
    if ($existing -split ';' | Where-Object { $_ -eq $dir }) { continue }
    $existing = if ($existing) { "$existing;$dir" } else { $dir }
    $added += $dir
}

if ($added.Count -eq 0) {
    Write-Host "No new toolchain paths to add (NASM/MinGW dirs missing or already on PATH)."
    exit 0
}

$scope = if ($Machine) { "Machine" } else { "User" }
try {
    [Environment]::SetEnvironmentVariable("Path", $existing, $scope)
    Write-Host "Added to $scope PATH:"
    $added | ForEach-Object { Write-Host "  $_" }
}
catch {
    Write-Warning "Could not update $scope PATH (admin may be required for Machine). Try without -Machine."
    throw
}

Write-Host ""
Write-Host "Open a new terminal, then: hla64 doctor"
Write-Host "For native Linux run on Windows, install Ubuntu WSL and gcc inside it."
