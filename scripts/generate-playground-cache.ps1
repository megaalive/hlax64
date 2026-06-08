# Regenerate docs/playground/cache/*.json from hla64 explain --json
# Run from repo root: ./scripts/generate-playground-cache.ps1

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$cacheDir = Join-Path $root 'docs/playground/cache'
New-Item -ItemType Directory -Force -Path $cacheDir | Out-Null

$examples = @(
    @{ id = 'hello';       path = 'examples/00-getting-started/hello.hla64' },
    @{ id = 'exitcode';    path = 'examples/00-getting-started/exitcode.hla64' },
    @{ id = 'arithmetic';  path = 'examples/01-arithmetic/simple.hla64' },
    @{ id = 'if-while';    path = 'examples/03-control-flow/count.hla64' },
    @{ id = 'procedure';   path = 'examples/04-procedures/add-two.hla64' },
    @{ id = 'stack-local'; path = 'examples/05-memory/stack-array.hla64' },
    @{ id = 'extern-puts'; path = 'examples/07-interop/extern-puts.hla64' },
    @{ id = 'linecount';   path = 'examples/10-real-tools/linecount/linecount.hla64' },
    @{ id = 'argv';        path = 'examples/10-real-tools/exists/exists.hla64' },
    @{ id = 'wc';          path = 'examples/10-real-tools/wc/wc.hla64' },
    @{ id = 'hexdump';     path = 'examples/10-real-tools/hexdump/hexdump.hla64' },
    @{ id = 'filemagic';   path = 'examples/10-real-tools/filemagic/filemagic.hla64' },
    @{ id = 'euler001';    path = 'examples/20-project-euler/problems/euler001-multiples-of-3-and-5-bruteforce.hla64' },
    @{ id = 'euler001-formula'; path = 'examples/20-project-euler/problems/euler001-multiples-of-3-and-5-formula.hla64' },
    @{ id = 'euler002';    path = 'examples/20-project-euler/problems/euler002-even-fibonacci.hla64' }
)

Push-Location $root
try {
    dotnet build src/HlaX64.Cli/HlaX64.Cli.csproj -v q
    foreach ($e in $examples) {
        $out = Join-Path $cacheDir "$($e.id).json"
        dotnet run --project src/HlaX64.Cli --no-build -- explain $e.path --json 2>$null | Set-Content -Encoding utf8 $out
        Write-Host "Wrote $($e.id).json"
    }
}
finally {
    Pop-Location
}
