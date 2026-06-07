# Git --msg-filter helper: drop Cursor agent attribution trailers from commit messages.
$text = [Console]::In.ReadToEnd()
if ([string]::IsNullOrEmpty($text)) { exit 0 }

$lines = $text -split "`r?`n"
$filtered = $lines | Where-Object {
    $_ -notmatch 'cursoragent@cursor\.com' -and
    $_ -notmatch 'Co-authored-by:\s*Cursor' -and
    $_ -notmatch 'Made-with:\s*Cursor'
}

# Trim trailing blank lines left after removing trailers.
while ($filtered.Count -gt 0 -and [string]::IsNullOrWhiteSpace($filtered[-1])) {
    $filtered = $filtered[0..($filtered.Count - 2)]
}

if ($filtered.Count -eq 0) {
    [Console]::Out.Write($text)
} else {
    [Console]::Out.Write(($filtered -join "`n"))
    if (-not $text.EndsWith("`n")) {
        # Preserve git's usual single trailing newline when input had none.
    } else {
        [Console]::Out.Write("`n")
    }
}
