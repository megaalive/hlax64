# Regenerate docs/playground/cache/<category>/*.json from hla64 explain --json
# Run from repo root: ./scripts/generate-playground-cache.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$manifestPath = Join-Path $root 'docs/playground/manifest.json'
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

Push-Location $root
try {
    dotnet build src/HlaX64.Cli/HlaX64.Cli.csproj -v q
    foreach ($cat in $manifest.categories) {
        $cacheDir = Join-Path $root "docs/playground/cache/$($cat.id)"
        New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null
        foreach ($e in $cat.examples) {
            $out = Join-Path $cacheDir "$($e.id).json"
            dotnet run --project src/HlaX64.Cli --no-build -- explain $e.path --json 2>$null | Set-Content -Encoding utf8 $out
            Write-Host "Wrote $($cat.id)/$($e.id).json"
        }
    }
}
finally {
    Pop-Location
}
