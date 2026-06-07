# Publish HlaX64.McpServer for Cursor MCP (ReadyToRun = faster cold start).
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$outDir = Join-Path $repoRoot '.cursor\mcp-hla64'

dotnet publish (Join-Path $repoRoot 'src\HlaX64.McpServer\HlaX64.McpServer.csproj') `
    -c $Configuration `
    -r win-x64 `
    -o $outDir `
    -p:PublishReadyToRun=true `
    -p:SelfContained=false

Write-Host "MCP server published to $outDir"
Write-Host "Reload MCP servers in Cursor (Settings -> MCP)."
