# Publish HlaX64 Assembly Lab for one or more RIDs.
# Usage:
#   .\scripts\publish-assembly-lab.ps1
#   .\scripts\publish-assembly-lab.ps1 -Rids win-x64,linux-x64 -Configuration Release

param(
    [string[]]$Rids = @("win-x64", "linux-x64"),
    [string]$Configuration = "Release",
    [string]$OutputRoot = "publish/assembly-lab",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $ProjectRoot "src/HlaX64.AssemblyLab/HlaX64.AssemblyLab.csproj"
$CliProject = Join-Path $ProjectRoot "src/HlaX64.Cli/HlaX64.Cli.csproj"
$McpProject = Join-Path $ProjectRoot "src/HlaX64.McpServer/HlaX64.McpServer.csproj"

function Copy-DirectoryContents([string]$Source, [string]$Destination) {
    if (-not (Test-Path $Source)) { return }
    New-Item -ItemType Directory -Force -Path $Destination | Out-Null
    Copy-Item (Join-Path $Source "*") $Destination -Recurse -Force
}

function Write-BundleManifest([string]$Rid, [string]$OutDir) {
    $manifest = [ordered]@{
        name = "HlaX64 Assembly Lab"
        rid = $Rid
        configuration = $Configuration
        selfContained = [bool]$SelfContained
        layoutVersion = 1
        includes = @(
            "assembly-lab",
            "hla64-cli",
            "mcp-server",
            "runtime",
            "docs",
            "examples",
            "optional-tools"
        )
        environment = @{
            HLAX64_RUNTIME_DIR = "runtime"
            PATH = "."
        }
    }
    $manifest | ConvertTo-Json -Depth 5 | Out-File -Encoding utf8 (Join-Path $OutDir "bundle-manifest.json")
}

function Write-InstallScripts([string]$Rid, [string]$OutDir) {
    if ($rid.StartsWith("win")) {
        @'
param(
    [string]$Destination = "$env:LOCALAPPDATA\HlaX64\AssemblyLab",
    [switch]$AddToUserPath
)

$ErrorActionPreference = "Stop"
$source = Split-Path -Parent $MyInvocation.MyCommand.Path
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Copy-Item (Join-Path $source "*") $Destination -Recurse -Force

$shortcutDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $shortcutDir "HlaX64 Assembly Lab.lnk"
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = Join-Path $Destination "HlaX64.AssemblyLab.exe"
$shortcut.WorkingDirectory = $Destination
$shortcut.Save()

if ($AddToUserPath) {
    $current = [Environment]::GetEnvironmentVariable("PATH", "User")
    if (($current -split ';') -notcontains $Destination) {
        [Environment]::SetEnvironmentVariable("PATH", "$Destination;$current", "User")
    }
}

Write-Host "Installed HlaX64 Assembly Lab to $Destination"
Write-Host "Run: $($shortcut.TargetPath)"
'@ | Out-File -Encoding utf8 (Join-Path $OutDir "install.ps1")
    } else {
        @'
#!/bin/sh
set -eu
DEST="${1:-$HOME/.local/share/hlax64/assembly-lab}"
BIN_DIR="${2:-$HOME/.local/bin}"
SRC="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"

mkdir -p "$DEST" "$BIN_DIR"
cp -R "$SRC"/. "$DEST"/
ln -sf "$DEST/hla64.sh" "$BIN_DIR/hla64"
cat > "$HOME/.local/share/applications/hlax64-assembly-lab.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=HlaX64 Assembly Lab
Exec=$DEST/HlaX64.AssemblyLab
Path=$DEST
Terminal=false
Categories=Development;
EOF

echo "Installed HlaX64 Assembly Lab to $DEST"
echo "CLI wrapper: $BIN_DIR/hla64"
echo "Ensure $BIN_DIR is on PATH."
'@ | Out-File -Encoding utf8 (Join-Path $OutDir "install.sh")
    }
}

Write-Host "=== Publish Assembly Lab ($Configuration) ===" -ForegroundColor Cyan

foreach ($rid in $Rids) {
    $outDir = Join-Path $ProjectRoot (Join-Path $OutputRoot $rid)
    $cliDir = Join-Path $outDir "cli"
    $mcpDir = Join-Path $outDir "mcp"
    Write-Host "Publishing $rid -> $outDir" -ForegroundColor Yellow
    if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    dotnet publish $Project -c $Configuration -r $rid --self-contained:$([bool]$SelfContained) -o $outDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish $CliProject -c $Configuration -r $rid --self-contained:$([bool]$SelfContained) -o $cliDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet publish $McpProject -c $Configuration -r $rid --self-contained:$([bool]$SelfContained) -o $mcpDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Copy-DirectoryContents (Join-Path $ProjectRoot "src/HlaX64.Runtime") (Join-Path $outDir "runtime")
    Copy-DirectoryContents (Join-Path $ProjectRoot "docs") (Join-Path $outDir "docs")
    Copy-DirectoryContents (Join-Path $ProjectRoot "examples") (Join-Path $outDir "examples")
    Copy-DirectoryContents (Join-Path $ProjectRoot "third_party/tools/$rid") (Join-Path $outDir "tools")

    Copy-Item (Join-Path $ProjectRoot "LICENSE") $outDir -Force
    Copy-Item (Join-Path $ProjectRoot "README.md") $outDir -Force
    Copy-Item (Join-Path $ProjectRoot "RELEASE_NOTES.md") $outDir -Force

    if ($rid.StartsWith("win")) {
        @"
@echo off
set "HLAX64_RUNTIME_DIR=%~dp0runtime"
set "PATH=%~dp0;%~dp0tools;%~dp0cli;%PATH%"
"%~dp0cli\HlaX64.Cli.exe" %*
"@ | Out-File -Encoding ascii (Join-Path $outDir "hla64.cmd")
    } else {
        @'
#!/bin/sh
DIR="$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)"
export HLAX64_RUNTIME_DIR="$DIR/runtime"
export PATH="$DIR:$DIR/tools:$DIR/cli:$PATH"
exec "$DIR/cli/HlaX64.Cli" "$@"
'@ | Out-File -Encoding utf8 (Join-Path $outDir "hla64.sh")
    }

    Write-BundleManifest $rid $outDir
    Write-InstallScripts $rid $outDir
}

Write-Host ""
Write-Host "=== Assembly Lab publish complete ===" -ForegroundColor Green
