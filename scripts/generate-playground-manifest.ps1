# Regenerate docs/playground/manifest.json from examples/ tree.
# Run from repo root: ./scripts/generate-playground-manifest.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$examplesRoot = Join-Path $root 'examples'
$outPath = Join-Path $root 'docs/playground/manifest.json'

function Humanize([string]$name) {
    $t = $name -replace '-', ' '
    return (Get-Culture).TextInfo.ToTitleCase($t)
}

function Get-ExampleId([string]$relFromExamples, [string]$baseName) {
    $parts = $relFromExamples -split '/'
    if ($relFromExamples -like 'tools/10-windows/exists/*') { return 'argv' }
    if ($relFromExamples -like 'tools/10-windows/*') { return $baseName }
    if ($relFromExamples -like 'tools/12-linux/*') { return "$baseName-linux" }
    if ($relFromExamples -like 'project-euler/problems/euler001-*-bruteforce*') { return 'euler001' }
    if ($relFromExamples -like 'project-euler/problems/euler001-*-formula*') { return 'euler001-formula' }
    if ($relFromExamples -match 'project-euler/problems/euler(\d{3})-') {
        $n = [int]$Matches[1]
        if ($n -le 25 -and $relFromExamples -notmatch '-template') {
            return ('euler{0:D3}' -f $n)
        }
    }
    if ($relFromExamples -like 'curriculum/00-getting-started/hello*') { return 'hello' }
    if ($relFromExamples -like 'curriculum/00-getting-started/exitcode*') { return 'exitcode' }
    if ($relFromExamples -like 'curriculum/01-arithmetic/simple*') { return 'arithmetic' }
    if ($relFromExamples -like 'curriculum/03-control-flow/count*') { return 'if-while' }
    if ($relFromExamples -like 'curriculum/04-procedures/add-two*') { return 'procedure' }
    if ($relFromExamples -like 'curriculum/05-memory/stack-array*') { return 'stack-local' }
    if ($relFromExamples -like 'interop/07-interop/extern-puts*') { return 'extern-puts' }
    if ($relFromExamples -like 'qa/bug-farm/*') {
        return ('bug-' + $parts[2] + '-' + $baseName) -replace '-+', '-'
    }
    if ($relFromExamples -like 'qa/invalid/*') {
        return ('invalid-' + $parts[2])
    }
    $slug = ($parts -join '-') -replace '\.hla64$', ''
    return ($slug -replace '[^a-zA-Z0-9_-]', '-').ToLowerInvariant()
}

function Get-RunCommand([string]$repoPath, [string]$toolDir, [string]$relFromExamples) {
    $argsFile = Join-Path $toolDir 'expected.arguments'
    if (Test-Path $argsFile) {
        $lines = @(Get-Content $argsFile | Where-Object { $_.Trim() -ne '' })
        if (($lines -join ' ') -match '\$HOST|\$PORT') {
            return "hla64 run $repoPath -- 127.0.0.1 <port>"
        }
        if ($relFromExamples -match '/grep/' -and $lines.Count -ge 2) {
            return ('hla64 run {0} -- {1} {2}' -f $repoPath, $lines[0], $lines[1])
        }
        if ($relFromExamples -match '/cmp/' -and $lines.Count -ge 2) {
            return ('hla64 run {0} -- {1} {2}' -f $repoPath, $lines[0], $lines[1])
        }
        if ($relFromExamples -match '(^|/)cp/' -and $lines.Count -ge 2) {
            return ('hla64 run {0} -- {1} {2}' -f $repoPath, $lines[0], $lines[1])
        }
        return ('hla64 run {0} -- {1}' -f $repoPath, $lines[0])
    }
    if ($relFromExamples -match '/tee/') {
        return "echo hello | hla64 run $repoPath -- out.txt"
    }
    if ($relFromExamples -match '/httpget/') {
        return "hla64 run $repoPath -- 127.0.0.1 <port> /"
    }
    if ($relFromExamples -match '/tcpget/') {
        return "hla64 run $repoPath -- 127.0.0.1 <port>"
    }
    return "hla64 run $repoPath"
}

function Get-ExpectedText([string]$toolDir, [string]$relFromExamples) {
    $stdoutFile = Join-Path $toolDir 'expected.stdout'
    if (-not (Test-Path $stdoutFile)) {
        $alt = Join-Path $toolDir 'expected.output'
        if (Test-Path $alt) { $stdoutFile = $alt }
    }
    $exitFile = Join-Path $toolDir 'expected.exitcode'
    $parts = @()
    if (Test-Path $stdoutFile) {
        $stdout = (Get-Content $stdoutFile -Raw)
        if ($null -ne $stdout) { $stdout = $stdout.Trim() }
        if ($stdout -and $stdout.Length -gt 0) {
            if ($stdout.Length -gt 400) { $stdout = $stdout.Substring(0, 400) + '…' }
            $parts += "stdout:`n  $stdout"
        }
    }
    elseif ($relFromExamples -like 'qa/invalid/*') {
        $parts += 'Expected: compile-time diagnostic(s) in Explain tab (success: false).'
    }
    elseif ($relFromExamples -match '-template\.hla64$') {
        $parts += 'Project Euler stub template — implement and compare with expected/ answers.'
    }
    elseif ($relFromExamples -match '/netcheck/' -or $relFromExamples -match '/httpget/' -or $relFromExamples -match '/tcpget/') {
        $parts += 'Requires a local TCP fixture; explain-only in browser unless you start a test server.'
    }
    else {
        $parts += '(run locally to inspect stdout and exit code)'
    }
    if (Test-Path $exitFile) {
        $code = Get-Content $exitFile -Raw
        if ($null -ne $code) { $code = $code.Trim() }
        if ($code -and $code.Length -gt 0) { $parts += "exit code: $code" }
    }
    return ($parts -join "`n")
}

function Get-Note([string]$relFromExamples) {
    if ($relFromExamples -like 'qa/invalid/*') {
        return 'Negative example — cached explain JSON includes diagnostics, not runnable NASM.'
    }
    if ($relFromExamples -match '/netcheck/' -or $relFromExamples -match '/httpget/' -or $relFromExamples -match '/tcpget/') {
        return 'Local-run-required; not fetched from the public internet.'
    }
    if ($relFromExamples -like 'interop/11-csharp-interop-real/*') {
        return 'Build as native DLL; see folder README for C# caller.'
    }
    if ($relFromExamples -like 'tools/12-linux/*') {
        return 'Linux SysV build — run on Linux or WSL.'
    }
    if ($relFromExamples -match '/loadavg/' -and $relFromExamples -like 'tools/10-windows/*') {
        return 'Windows: unsupported at runtime; Linux returns load average.'
    }
    return $null
}

$categoryMeta = [ordered]@{
    'curriculum'     = @{ label = 'Curriculum'; groupLabels = @{
        '00-getting-started' = '00 Getting started'
        '01-arithmetic'      = '01 Arithmetic'
        '02-types'           = '02 Types'
        '03-control-flow'    = '03 Control flow'
        '04-procedures'      = '04 Procedures'
        '05-memory'          = '05 Memory'
        '06-abi'             = '06 ABI'
        '08-ai-agent'        = '08 AI agent'
    } }
    'interop'        = @{ label = 'Interop'; groupLabels = @{
        '07-interop'              = '07 C / extern'
        '11-csharp-interop-real'  = '11 C# interop'
    } }
    'tools'          = @{ label = 'Tools'; groupLabels = @{
        '10-windows' = '10 Windows'
        '12-linux'   = '12 Linux'
    } }
    'project-euler'  = @{ label = 'Project Euler'; groupLabels = @{
        'problems' = 'Problems'
        'runner'   = 'Runner'
    } }
    'qa'             = @{ label = 'QA'; groupLabels = @{
        'bug-farm' = 'Bug farm'
        'invalid'  = 'Invalid (must not compile)'
    } }
}

$groups = @{}
foreach ($top in $categoryMeta.Keys) {
    $groups[$top] = @{}
}

Get-ChildItem -Path $examplesRoot -Recurse -Filter '*.hla64' | Sort-Object FullName | ForEach-Object {
    $rel = $_.FullName.Substring($examplesRoot.Length + 1).Replace('\', '/')
    $parts = $rel -split '/'
    $top = $parts[0]
    if ($categoryMeta.Keys -notcontains $top) { return }

    $groupKey = if ($parts.Count -ge 3) { $parts[1] } else { '_' }
    if ($top -eq 'project-euler') {
        $groupKey = if ($parts[1] -eq 'runner') { 'runner' } else { 'problems' }
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($_.Name)
    $repoPath = "examples/$rel"
    $toolDir = $_.DirectoryName
    $id = Get-ExampleId $rel $baseName

    $entry = [ordered]@{
        id       = $id
        label    = Humanize $baseName
        path     = $repoPath
        run      = Get-RunCommand $repoPath $toolDir $rel
        expected = Get-ExpectedText $toolDir $rel
    }
    $note = Get-Note $rel
    if ($note) { $entry.note = $note }

    if (-not ($groups[$top].Keys -contains $groupKey)) {
        $groups[$top][$groupKey] = @()
    }
    $groups[$top][$groupKey] += ,$entry
}

$categories = @()
foreach ($top in $categoryMeta.Keys) {
    $meta = $categoryMeta[$top]
    $catGroups = @()
    foreach ($gk in ($groups[$top].Keys | Sort-Object)) {
        $label = if ($meta.groupLabels.Keys -contains $gk) { $meta.groupLabels[$gk] }
                 elseif ($gk -eq '_') { $meta.label }
                 else { Humanize $gk }
        $catGroups += [ordered]@{
            id       = $gk
            label    = $label
            examples = $groups[$top][$gk]
        }
    }
    $categories += [ordered]@{
        id     = $top
        label  = $meta.label
        groups = $catGroups
    }
}

$manifest = [ordered]@{ categories = $categories }
$json = $manifest | ConvertTo-Json -Depth 8
[System.IO.File]::WriteAllText($outPath, $json + "`n", [System.Text.UTF8Encoding]::new($false))

$count = ($categories | ForEach-Object { $_.groups | ForEach-Object { $_.examples.Count } } | Measure-Object -Sum).Sum
Write-Host "Wrote $outPath ($count examples in $($categories.Count) categories)"
