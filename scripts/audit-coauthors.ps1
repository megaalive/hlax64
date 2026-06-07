# Audit git history for Cursor agent co-author trailers.
# Usage: powershell -File scripts/audit-coauthors.ps1 [-Branch main]

param(
    [string]$Branch = "main"
)

$pattern = "cursoragent@cursor\.com|Co-authored-by:\s*Cursor"
$hits = @()

git log $Branch --format="%H" | ForEach-Object {
    $body = git log -1 --format="%B" $_
    if ($body -match $pattern) {
        $hits += [PSCustomObject]@{
            Commit = git log -1 --format="%h %s" $_
            Author = git log -1 --format="%an <%ae>" $_
        }
    }
}

if ($hits.Count -eq 0) {
    Write-Host "OK: no Cursor co-author trailers on branch '$Branch'."
    exit 0
}

Write-Host "Found $($hits.Count) commit(s) with Cursor co-author on '$Branch':"
$hits | Format-Table -AutoSize
exit 1
