param(
    [string]$RepoRoot = (Split-Path -Parent $PSScriptRoot)
)

$codeGraphProject = Join-Path $RepoRoot "..\CodeGraphCodex\src\CodeGraphCodex\CodeGraphCodex.csproj"
if (-not (Test-Path $codeGraphProject)) {
    $codeGraphProject = "D:\_2025\Gits\megaalive\CodeGraphCodex\src\CodeGraphCodex\CodeGraphCodex.csproj"
}

$outFile = Join-Path $RepoRoot ".codegraph\codegraph.json"
New-Item -ItemType Directory -Force -Path (Split-Path $outFile) | Out-Null

Write-Host "CodeGraph: indexing $RepoRoot"
dotnet run --project $codeGraphProject -- index $RepoRoot --out $outFile
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Host "CodeGraph: wrote $outFile"
