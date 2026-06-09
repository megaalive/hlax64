# Verify conformance manifests match current compiler output.
# Run after changing NasmEmitter, IR lowering, or semantics that affect emitted text.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Project = Join-Path $Root "tests/HlaX64.Compiler.Tests/HlaX64.Compiler.Tests.csproj"

Write-Host "Conformance: running ConformanceTests ($Configuration)..."
dotnet test $Project -c $Configuration --filter "FullyQualifiedName~ConformanceTests" --nologo
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Conformance drift detected. Update tests/conformance/*/manifest.json in the same PR as emitter changes."
    Write-Host "See tests/conformance/README.md"
    exit $LASTEXITCODE
}

Write-Host "Conformance: all cases passed."
