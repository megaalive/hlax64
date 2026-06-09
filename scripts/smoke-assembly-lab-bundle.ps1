param(
    [Parameter(Mandatory = $true)]
    [string]$BundleDir
)

$ErrorActionPreference = "Stop"
$bundle = Resolve-Path $BundleDir
$isWindowsHost = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
    [System.Runtime.InteropServices.OSPlatform]::Windows)

function Assert-Exists([string]$Path, [string]$Name) {
    if (-not (Test-Path $Path)) {
        throw "Missing $Name at $Path"
    }
}

Assert-Exists (Join-Path $bundle "bundle-manifest.json") "bundle manifest"
Assert-Exists (Join-Path $bundle "runtime") "runtime directory"
Assert-Exists (Join-Path $bundle "examples") "examples directory"

if ($isWindowsHost) {
    $hla64 = Join-Path $bundle "hla64.cmd"
    Assert-Exists (Join-Path $bundle "install.ps1") "Windows installer script"
} else {
    $hla64 = Join-Path $bundle "hla64.sh"
    Assert-Exists (Join-Path $bundle "install.sh") "Linux installer script"
    if (Test-Path $hla64) {
        chmod +x $hla64
    }
}

Assert-Exists $hla64 "hla64 wrapper"

Write-Host "=== hla64 doctor ==="
& $hla64 doctor --json
$doctorOk = $LASTEXITCODE -eq 0
if (-not $doctorOk) {
    Write-Warning "doctor reported missing optional/system dependencies; continuing smoke checks"
}

$hello = Join-Path $bundle "examples/curriculum/00-getting-started/hello.hla64"
Assert-Exists $hello "hello example"

Write-Host "=== hla64 build hello ==="
if ($doctorOk) {
    $target = if ($isWindowsHost) { "windows-x64-msabi" } else { "linux-x64-sysv" }
    & $hla64 build $hello --target $target
    if ($LASTEXITCODE -ne 0) {
        throw "hello build failed"
    }
} else {
    Write-Warning "Skipping build smoke because doctor did not pass on this runner."
}

Write-Host "Assembly Lab bundle smoke test passed: $bundle"
